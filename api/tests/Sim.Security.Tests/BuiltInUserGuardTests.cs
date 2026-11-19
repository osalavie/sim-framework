using System.Reflection;
using Sim.Framework.Application.Mapping;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.SqlSugar.Migrations;
using Sim.System.SqlSugar.Repositories;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Application.User;
using Sim.System.Domain.E2ee;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;
using Sim.System.SqlSugar.Repositories.Role;
using Sim.System.SqlSugar.Repositories.User;

namespace Sim.Security.Tests;

/// <summary>
/// 内置账号（初始化管理员与系统账号）的身份保护：账号名与角色不可变、禁止删除，其余资料仍可编辑。
/// </summary>
public sealed class BuiltInUserGuardTests
{
    static BuiltInUserGuardTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task UpdateAsync_allows_profile_changes_for_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var admin = CreateUser("fausto", isBuiltIn: true);
        await database.Insertable(admin).ExecuteCommandAsync();
        await database.Insertable(CreateAdminRole()).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var updated = await service.UpdateAsync(ToUpdateDto(admin, nickName: "运维负责人"));

        Assert.Equal("运维负责人", updated.NickName);
        Assert.Equal("fausto", updated.UserName);
    }

    [Fact]
    public async Task UpdateAsync_blocks_rename_of_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var admin = CreateUser("fausto", isBuiltIn: true);
        await database.Insertable(admin).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(ToUpdateDto(admin, userName: "someone-else")));

        Assert.Equal(SystemErrorCodes.BuiltInUserProtected, exception.Code);
        Assert.Null(await database.Queryable<UserEntity>().FirstAsync(u => u.Id == admin.Id && u.UserName == "someone-else"));
    }

    [Fact]
    public async Task UpdateAsync_blocks_role_changes_for_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var admin = CreateUser("fausto", isBuiltIn: true);
        var adminRole = CreateAdminRole();
        var editorRole = CreateRole("Editor");
        await database.Insertable(admin).ExecuteCommandAsync();
        await database.Insertable(new[] { adminRole, editorRole }).ExecuteCommandAsync();
        await database.Insertable(new UserRole { Id = Guid.NewGuid(), UserId = admin.Id, RoleId = adminRole.Id })
            .ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(ToUpdateDto(admin, roleIds: [editorRole.Id])));

        Assert.Equal(SystemErrorCodes.BuiltInUserProtected, exception.Code);
        var links = await database.Queryable<UserRole>().Where(link => link.UserId == admin.Id).ToListAsync();
        Assert.Equal([adminRole.Id], links.Select(link => link.RoleId));
    }

    [Fact]
    public async Task DeleteAsync_blocks_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var admin = CreateUser("fausto", isBuiltIn: true);
        await database.Insertable(admin).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(admin.Id));

        Assert.Equal(SystemErrorCodes.BuiltInUserProtected, exception.Code);
        Assert.NotNull(await database.Queryable<UserEntity>().FirstAsync(u => u.Id == admin.Id));
    }

    [Fact]
    public async Task ResetUserPasswordAsync_blocks_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var admin = CreateUser("fausto", isBuiltIn: true);
        await database.Insertable(admin).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ResetUserPasswordAsync(admin.Id));

        Assert.Equal(SystemErrorCodes.BuiltInUserProtected, exception.Code);
        var stored = await database.Queryable<UserEntity>().FirstAsync(u => u.Id == admin.Id);
        Assert.Equal(admin.EncryptedPassword, stored.EncryptedPassword);
        Assert.Equal(admin.Salt, stored.Salt);
    }

    [Fact]
    public async Task UpdateAsync_allows_rename_of_regular_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var editor = CreateUser("editor-one");
        await database.Insertable(editor).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var updated = await service.UpdateAsync(ToUpdateDto(editor, userName: "editor-two"));

        Assert.Equal("editor-two", updated.UserName);
    }

    [Fact]
    public async Task DeleteAsync_allows_regular_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserPreferenceEntity, UserE2eeIdentityEntity>();
        var editor = CreateUser("editor-one");
        var adminRole = CreateAdminRole();
        await database.Insertable(editor).ExecuteCommandAsync();
        await database.Insertable(adminRole).ExecuteCommandAsync();
        await database.Insertable(new UserRole { Id = Guid.NewGuid(), UserId = editor.Id, RoleId = adminRole.Id })
            .ExecuteCommandAsync();
        var service = CreateService(database, out var revoker);

        Assert.True(await service.DeleteAsync(editor.Id));

        Assert.Null(await database.Queryable<UserEntity>().FirstAsync(u => u.Id == editor.Id));
        Assert.Empty(await database.Queryable<UserRole>().Where(link => link.UserId == editor.Id).ToListAsync());
        Assert.Contains(editor.Id, revoker.Revoked);
    }

    [Fact]
    public async Task Migration_marks_host_administrators_built_in_only_once_and_skips_tenants()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var adminRole = CreateAdminRole();
        var editorRole = CreateRole("Editor");
        var administrator = CreateUser("fausto");
        var tenantAdministrator = CreateUser("tenant-admin");
        tenantAdministrator.TenantId = Guid.NewGuid();
        var editor = CreateUser("editor");
        await database.Insertable(new[] { adminRole, editorRole }).ExecuteCommandAsync();
        await database.Insertable(new[] { administrator, tenantAdministrator, editor }).ExecuteCommandAsync();
        await database.Insertable(new[]
        {
            new UserRole { Id = Guid.NewGuid(), UserId = administrator.Id, RoleId = adminRole.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = tenantAdministrator.Id, RoleId = adminRole.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = editor.Id, RoleId = editorRole.Id }
        }).ExecuteCommandAsync();

        var migration = new HostAdministratorBuiltInMigration();
        await migration.UpAsync(database);
        await migration.UpAsync(database);

        var rows = await database.Queryable<UserEntity>().ToListAsync();
        Assert.True(rows.Single(user => user.Id == administrator.Id).IsBuiltIn);
        Assert.False(rows.Single(user => user.Id == tenantAdministrator.Id).IsBuiltIn);
        Assert.False(rows.Single(user => user.Id == editor.Id).IsBuiltIn);
    }

    private static UserService CreateService(ISqlSugarClient database, out RecordingSessionRevoker revoker)
    {
        revoker = new RecordingSessionRevoker();
        return new UserService(
            null!,
            new UserRepository(database),
            new UserRoleRepository(database),
            null!,
            null!,
            null!,
            new RoleRepository(database),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            revoker,
            null!,
            null!,
            new SqlSugarRepository<UserPreferenceEntity>(database),
            new SqlSugarRepository<UserE2eeIdentityEntity>(database),
            null!,
            null!)
        {
            ObjectMapper = new MapsterObjectMapper()
        };
    }

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"builtin-user-guard-tests-{Guid.NewGuid():N}",
        DbType = DbType.Sqlite,
        ConnectionString = "DataSource=:memory:",
        IsAutoCloseConnection = false,
        ConfigureExternalServices = new ConfigureExternalServices
        {
            EntityService = (property, column) =>
            {
                if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    column.IsPrimarykey = true;
                }

                if (property.Name == "IdType")
                {
                    column.IsIgnore = true;
                }

                if (column.IsPrimarykey == false &&
                    new NullabilityInfoContext().Create(property).WriteState is NullabilityState.Nullable)
                {
                    column.IsNullable = true;
                }
            }
        }
    });

    private static UpdateUserDto ToUpdateDto(
        UserEntity user,
        string? userName = null,
        string? nickName = null,
        List<Guid>? roleIds = null) => new()
    {
        Id = user.Id,
        UserName = userName ?? user.UserName,
        NickName = nickName ?? user.NickName ?? user.UserName,
        Email = user.Email ?? string.Empty,
        RoleIds = roleIds
    };

    private static UserEntity CreateUser(string userName, bool isBuiltIn = false) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        NickName = userName,
        Name = userName,
        Email = $"{userName}@example.com",
        EncryptedPassword = "hash",
        Salt = string.Empty,
        IsActive = true,
        IsBuiltIn = isBuiltIn,
        CreationTime = DateTime.UtcNow
    };

    private static RoleEntity CreateAdminRole() => CreateRole(RoleConst.Admin, isBuiltIn: true);

    private static RoleEntity CreateRole(string code, bool isBuiltIn = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = code,
        Code = code,
        Description = string.Empty,
        IsBuiltIn = isBuiltIn,
        CreationTime = DateTime.UtcNow
    };

    private sealed class RecordingSessionRevoker : IUserSessionRevoker
    {
        public List<Guid> Revoked { get; } = [];

        public Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds)
        {
            Revoked.AddRange(userIds);
            return Task.CompletedTask;
        }

        public Task<int> RevokeSessionAsync(Guid userId, Guid sessionId)
        {
            Revoked.Add(userId);
            return Task.FromResult(1);
        }
    }
}
