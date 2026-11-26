using System.Reflection;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.SqlSugar.Migrations;
using Sim.System.SqlSugar.Repositories;
using SqlSugar;
using Sim.System.Application.User;
using Sim.System.Domain.User;
using Sim.System.SqlSugar.Repositories.User;

namespace Sim.Security.Tests;

public sealed class UserUniqueValidationTests
{
    static UserUniqueValidationTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task EnsureUserIsUniqueAsync_allows_new_user()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        var service = CreateService(database);

        await service.EnsureUserIsUniqueAsync("new-user", "new@example.com");
    }

    [Fact]
    public async Task EnsureUserIsUniqueAsync_rejects_existing_username()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        await database.Insertable(CreateUser("existing-user", "existing@example.com")).ExecuteCommandAsync();
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureUserIsUniqueAsync("existing-user", "other@example.com"));

        Assert.Equal(SystemErrorCodes.UserNameAlreadyExists, exception.Code);
    }

    [Fact]
    public async Task EnsureUserIsUniqueAsync_rejects_email_owned_by_another_user()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        await database.Insertable(CreateUser("existing-user", "existing@example.com")).ExecuteCommandAsync();
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureUserIsUniqueAsync("new-user", "existing@example.com"));

        Assert.Equal(SystemErrorCodes.EmailAlreadyExists, exception.Code);
    }

    [Fact]
    public async Task EnsureUserIsUniqueAsync_allows_update_to_keep_current_username_and_email()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        var user = CreateUser("existing-user", "existing@example.com");
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateService(database);

        await service.EnsureUserIsUniqueAsync(user.UserName, user.Email, user.Id);
    }

    [Fact]
    public async Task EnsureNickNameIsUniqueAsync_rejects_existing_nickname()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        await database.Insertable(CreateUser("existing-user", "existing@example.com", "alice"))
            .ExecuteCommandAsync();
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.EnsureNickNameIsUniqueAsync("  alice  "));

        Assert.Equal(SystemErrorCodes.NickNameAlreadyExists, exception.Code);
    }

    [Fact]
    public async Task EnsureNickNameIsUniqueAsync_allows_own_nickname()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        var user = CreateUser("existing-user", "existing@example.com", "alice");
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateService(database);

        await service.EnsureNickNameIsUniqueAsync(user.NickName, user.Id);
    }

    [Fact]
    public async Task EnsureNickNameIsUniqueAsync_rejects_empty_nickname()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        var service = CreateService(database);

        foreach (var value in new string?[] { null, string.Empty, "   " })
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() =>
                service.EnsureNickNameIsUniqueAsync(value));
            Assert.Equal(SystemErrorCodes.NickNameRequired, exception.Code);
        }
    }

    [Fact]
    public async Task GetNickNameAvailabilityAsync_reports_taken_and_free_values()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();
        var user = CreateUser("existing-user", "existing@example.com", "alice");
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateService(database);

        Assert.False(await service.GetNickNameAvailabilityAsync(" alice "));
        Assert.False(await service.GetNickNameAvailabilityAsync("alice", Guid.NewGuid()));
        Assert.True(await service.GetNickNameAvailabilityAsync("alice", user.Id));
        Assert.True(await service.GetNickNameAvailabilityAsync("bob"));
        Assert.False(await service.GetNickNameAvailabilityAsync("   "));
    }

    [Fact]
    public async Task NickName_migration_deduplicates_and_creates_unique_index()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity>();

        var older = CreateUser("older", "older@example.com", "alice");
        older.CreationTime = DateTime.UtcNow.AddMinutes(-2);
        var newer = CreateUser("newer", "newer@example.com", "alice");
        var empty = CreateUser("empty", "empty@example.com", "   ");
        await database.Insertable(new[] { older, newer, empty }).ExecuteCommandAsync();

        var migration = new UserNickNameUniqueMigration();
        await migration.UpAsync(database);

        var rows = await database.Queryable<UserEntity>().ToListAsync();
        Assert.Contains(rows, user => user.NickName == "alice");
        Assert.Contains(rows, user => user.NickName == "alice-2");
        Assert.Contains(rows, user => user.NickName == null);
        Assert.True(database.DbMaintenance.IsAnyIndex("idx_system_user_nick_name"));

        await migration.UpAsync(database);
        Assert.Equal(3, await database.Queryable<UserEntity>().CountAsync());
    }

    private static UserService CreateService(ISqlSugarClient database)
    {
        IUserRepository repository = new UserRepository(database);
        return new UserService(
            null!,
            repository,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static UserEntity CreateUser(string userName, string email, string? nickName = null) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        NickName = nickName,
        Email = email,
        EncryptedPassword = "hash",
        Salt = string.Empty,
        IsActive = true,
        CreationTime = DateTime.UtcNow
    };

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"user-unique-tests-{Guid.NewGuid():N}",
        DbType = DbType.Sqlite,
        ConnectionString = "DataSource=:memory:",
        IsAutoCloseConnection = false,
        ConfigureExternalServices = new ConfigureExternalServices
        {
            EntityService = (property, column) =>
            {
                if (property.Name.ToLowerInvariant() == "id")
                {
                    column.IsPrimarykey = true;
                }

                if (property.Name == "IdType")
                {
                    column.IsIgnore = true;
                }

                if (column.IsPrimarykey == false &&
                    new NullabilityInfoContext().Create(property).WriteState is
                        NullabilityState.Nullable)
                {
                    column.IsNullable = true;
                }
            }
        }
    });
}