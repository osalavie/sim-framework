using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sim.Framework.Application.Mapping;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.SqlSugar.Repositories;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Application.SystemConfig;
using Sim.System.Application.User;
using Sim.System.Domain.E2ee;
using Sim.System.Domain.Role;
using Sim.System.Domain.Security;
using Sim.System.Domain.User;
using Sim.System.SqlSugar.Repositories.Role;
using Sim.System.SqlSugar.Repositories.User;

namespace Sim.Security.Tests;

/// <summary>
/// 用户默认密码行为：管理端创建与重置都使用系统配置的默认密码，不接收调用方指定的密码。
/// </summary>
public sealed class UserPasswordManagementTests
{
    private const string DefaultPassword = "Default@123456";

    static UserPasswordManagementTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task CreateAsync_uses_configured_default_password_and_ignores_request_password()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var service = CreateService(database, out _, DefaultPassword);

        var created = await service.CreateAsync(new CreateUserDto
        {
            UserName = "new-user",
            Password = "Client@123456",
            NickName = "New User",
            Email = "new-user@example.com"
        });

        var stored = await database.Queryable<UserEntity>().FirstAsync(user => user.Id == created.Id);
        Assert.Equal($"encrypted:{DefaultPassword}", stored.EncryptedPassword);
        Assert.Equal("test-salt", stored.Salt);
    }

    [Fact]
    public async Task CreateAsync_notifies_user_created_handlers()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var handler = new RecordingUserCreatedHandler();
        var service = CreateService(database, out _, DefaultPassword, [handler]);

        var created = await service.CreateAsync(new CreateUserDto
        {
            UserName = "new-user",
            NickName = "新用户",
            Email = "new-user@example.com"
        });

        var context = Assert.Single(handler.Contexts);
        Assert.Equal(created.Id, context.UserId);
        Assert.Equal("new-user", context.UserName);
        Assert.Equal("新用户", context.NickName);
    }

    [Fact]
    public async Task CreateAsync_allows_blank_email_and_stores_null()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var service = CreateService(database, out _, DefaultPassword);

        var created = await service.CreateAsync(new CreateUserDto
        {
            UserName = "no-email-user",
            NickName = "无邮箱用户",
            Email = "   "
        });

        var stored = await database.Queryable<UserEntity>().FirstAsync(user => user.Id == created.Id);
        Assert.Null(stored.Email);
    }

    [Fact]
    public async Task UpdateAsync_allows_blank_email_and_clears_stored_email()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var user = CreateUser("blank-email-editor");
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateService(database, out _, DefaultPassword);

        await service.UpdateAsync(new UpdateUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            NickName = user.NickName!,
            Email = ""
        });

        var stored = await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id);
        Assert.Null(stored.Email);
    }

    [Fact]
    public async Task RegisterAsync_assigns_configured_roles_and_skips_built_in_roles()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var userRole = new RoleEntity { Id = Guid.NewGuid(), Name = "User", Code = "User" };
        var adminRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Code = "Admin",
            IsBuiltIn = true
        };
        await database.Insertable(new[] { userRole, adminRole }).ExecuteCommandAsync();
        var service = CreateService(database, out _, DefaultPassword, registrationRoleCodes: "User,Admin");

        var created = await service.RegisterAsync(new CreateUserDto
        {
            UserName = "self-register",
            NickName = "自助注册用户",
            Password = "Passw0rd!",
            Email = "self-register@example.com"
        });

        var links = await database.Queryable<UserRole>()
            .Where(link => link.UserId == created.Id)
            .ToListAsync();
        Assert.Contains(links, link => link.RoleId == userRole.Id);
        Assert.DoesNotContain(links, link => link.RoleId == adminRole.Id);
    }

    [Fact]
    public async Task RegisterAsync_falls_back_to_user_role_when_role_configuration_is_missing()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var userRole = new RoleEntity { Id = Guid.NewGuid(), Name = "User", Code = "User" };
        await database.Insertable(userRole).ExecuteCommandAsync();
        var service = CreateService(database, out _, DefaultPassword);

        var created = await service.RegisterAsync(new CreateUserDto
        {
            UserName = "fallback-register",
            NickName = "默认角色用户",
            Password = "Passw0rd!"
        });

        var links = await database.Queryable<UserRole>()
            .Where(link => link.UserId == created.Id)
            .ToListAsync();
        Assert.Contains(links, link => link.RoleId == userRole.Id);
    }

    [Fact]
    public async Task ResetUserPasswordAsync_uses_configured_default_password_and_revokes_sessions()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("editor-one");
        user.SecurityChallengeId = Guid.NewGuid();
        user.EncryptedPassword = "old-hash";
        user.Salt = "old-salt";
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateService(database, out var revoker, DefaultPassword);

        await service.ResetUserPasswordAsync(user.Id);

        var stored = await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id);
        Assert.Equal($"encrypted:{DefaultPassword}", stored.EncryptedPassword);
        Assert.Equal("test-salt", stored.Salt);
        Assert.Null(stored.SecurityChallengeId);
        Assert.Contains(user.Id, revoker.Revoked);
    }

    [Fact]
    public async Task CreateAsync_fails_when_default_password_is_not_configured()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity>();
        var service = CreateService(database, out _, defaultPassword: null);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new CreateUserDto
        {
            UserName = "new-user",
            NickName = "New User",
            Email = "new-user@example.com"
        }));

        Assert.Equal(SystemErrorCodes.DefaultUserPasswordNotConfigured, exception.Code);
    }

    [Fact]
    public async Task ChangePasswordAsync_clears_legacy_backup_without_requiring_rewrap()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("change-password-backup");
        user.EncryptedPassword = "encrypted:Old@123456";
        await database.Insertable(user).ExecuteCommandAsync();
        await database.Insertable(CreateIdentityWithAccountBackup(user.Id)).ExecuteCommandAsync();
        var service = CreateService(database, out _, DefaultPassword);
        service.CurrentUser = new TestCurrentUser(user.Id);

        await service.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = "Old@123456", NewPassword = "New@123456", ConfirmPassword = "New@123456"
        });
        var stored = await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id);
        Assert.Equal("encrypted:New@123456", stored.EncryptedPassword);
        var identity = await database.Queryable<UserE2eeIdentityEntity>().FirstAsync(row => row.UserId == user.Id);
        Assert.Null(identity.AccountWrappedRecoveryKey);
        Assert.Null(identity.AccountKdfSalt);
        Assert.Null(identity.AccountKdfIterations);
    }

    [Fact]
    public async Task ChangePasswordAsync_does_not_recreate_backup_from_old_client_fields()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("change-password-rewrap");
        user.EncryptedPassword = "encrypted:Old@123456";
        await database.Insertable(user).ExecuteCommandAsync();
        await database.Insertable(CreateIdentityWithAccountBackup(user.Id)).ExecuteCommandAsync();
        var service = CreateService(database, out _, DefaultPassword);
        service.CurrentUser = new TestCurrentUser(user.Id);

        var replacement = WrapEnvelope();
        var replacementSalt = Base64Url(16);
        await service.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = "Old@123456",
            NewPassword = "New@123456",
            ConfirmPassword = "New@123456",
            AccountWrappedRecoveryKey = replacement,
            AccountKdfSalt = replacementSalt,
            AccountKdfIterations = 600_000
        });

        var storedUser = await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id);
        Assert.Equal("encrypted:New@123456", storedUser.EncryptedPassword);

        var identity = await database.Queryable<UserE2eeIdentityEntity>()
            .FirstAsync(candidate => candidate.UserId == user.Id);
        Assert.Null(identity.AccountWrappedRecoveryKey);
        Assert.Null(identity.AccountKdfSalt);
        Assert.Null(identity.AccountKdfIterations);
        Assert.Null(identity.AccountRecoveryKeyUpdatedAt);
        Assert.Equal(2, identity.Revision);
    }

    private static UserE2eeIdentityEntity CreateIdentityWithAccountBackup(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        KdfAlgorithm = "PBKDF2-SM3",
        KdfSalt = Base64Url(16),
        KdfIterations = 600_000,
        PasswordWrappedKey = WrapEnvelope(),
        RecoveryWrappedKey = WrapEnvelope(),
        AccountWrappedRecoveryKey = WrapEnvelope(),
        AccountKdfSalt = Base64Url(16),
        AccountKdfIterations = 600_000,
        IsActive = true,
        Revision = 1
    };

    private static string WrapEnvelope() => $"wrap:v1:{Base64Url(44)}";

    private static string Base64Url(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i += 1)
        {
            bytes[i] = (byte)(i * 7);
        }

        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static UserService CreateService(
        ISqlSugarClient database,
        out RecordingSessionRevoker revoker,
        string? defaultPassword,
        IEnumerable<IUserCreatedHandler>? userCreatedHandlers = null,
        string? registrationRoleCodes = null)
    {
        revoker = new RecordingSessionRevoker();
        return new UserService(
            new StubSecurityManager(),
            new UserRepository(database),
            new UserRoleRepository(database),
            null!,
            null!,
            null!,
            new RoleRepository(database),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            null!,
            Options.Create(new SecurityOptions()),
            null!,
            null!,
            null!,
            null!,
            new StubConfigurationReader(defaultPassword, registrationRoleCodes),
            revoker,
            null!,
            null!,
            null!,
            new SqlSugarRepository<UserE2eeIdentityEntity>(database),
            new ConfigurationBuilder().Build(),
            NullLogger<UserService>.Instance,
            userCreatedHandlers)
        {
            ObjectMapper = new MapsterObjectMapper()
        };
    }

    private static UserEntity CreateUser(string userName) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        NickName = userName,
        Email = $"{userName}@example.com",
        EncryptedPassword = "hash",
        Salt = string.Empty,
        IsActive = true,
        CreationTime = DateTime.UtcNow
    };

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"user-password-management-tests-{Guid.NewGuid():N}",
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

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "password-tester";
        public string? Name => "Password";
        public string? SurName => "Tester";
        public string? Email => "password-tester@example.test";
        public bool EmailVerified => true;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => ["admin"];
        public Claim? FindClaim(string claimType) => null;
        public string? FindClaimValue(string claimType) => null;
        public Claim[] FindClaims(string claimType) => [];
        public Claim[] GetAllClaims() => [];
        public bool IsInRole(string roleName) => roleName == "admin";
    }

    private sealed class StubConfigurationReader(string? defaultPassword, string? registrationRoleCodes = null)
        : ISystemConfigurationReader
    {
        public Task<string?> GetValueAsync(string configKey) =>
            Task.FromResult(configKey switch
            {
                "security.user-default-password" => defaultPassword,
                "security.registration-enabled" => "true",
                "security.registration-role-codes" => registrationRoleCodes,
                _ => null
            });

        public Task<IReadOnlyDictionary<string, string>> GetValuesAsync(params string[] configKeys) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    private sealed class StubSecurityManager : ISecurityManager
    {
        public (bool IsValid, List<string> Errors) ValidatePassword(string password) =>
            (true, []);

        public (string EncryptedPassword, string Salt) EncryptPassword(string password) =>
            ($"encrypted:{password}", "test-salt");

        public bool VerifyPassword(UserEntity user, string password) =>
            user.EncryptedPassword == $"encrypted:{password}";

        public (string PublicKey, string PrivateKey) GenerateSm2KeyPair() => ("", "");

        public string GenerateAccessToken(IEnumerable<Claim> claims) => string.Empty;

        public string GenerateRefreshToken() => string.Empty;

        public string GenerateSm4Key() => string.Empty;
    }

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

    private sealed class RecordingUserCreatedHandler : IUserCreatedHandler
    {
        public List<UserCreatedContext> Contexts { get; } = [];

        public Task HandleAsync(UserCreatedContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }
}
