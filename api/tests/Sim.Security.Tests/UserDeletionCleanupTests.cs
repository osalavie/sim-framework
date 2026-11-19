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
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.FileManagement;
using Sim.System.SqlSugar.Repositories;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Application.User;
using Sim.System.Domain.E2ee;
using Sim.System.Domain.ExternalLogin;
using Sim.System.Domain.FileManagement;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Role;
using Sim.System.Domain.Security;
using Sim.System.Domain.User;
using Sim.System.SqlSugar.Repositories.ExternalLogin;
using Sim.System.SqlSugar.Repositories.Role;
using Sim.System.SqlSugar.Repositories.Security;
using Sim.System.SqlSugar.Repositories.User;

namespace Sim.Security.Tests;

/// <summary>
/// 删除用户时的数据清理：账号级数据硬删除，业务留痕由各模块的删除处理器接管。
/// </summary>
public sealed class UserDeletionCleanupTests
{
    static UserDeletionCleanupTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task DeleteAsync_invokes_user_deleted_handlers_before_removing_the_user_row()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserPreferenceEntity, UserE2eeIdentityEntity>();
        var editor = CreateUser("editor");
        await database.Insertable(editor).ExecuteCommandAsync();
        var handler = new RecordingUserDeletedHandler();
        var service = CreateUserService(database, [handler]);

        Assert.True(await service.DeleteAsync(editor.Id));

        var context = Assert.Single(handler.Contexts);
        Assert.Equal(editor.Id, context.UserId);
        Assert.Null(await database.Queryable<UserEntity>().FirstAsync(user => user.Id == editor.Id));
    }

    [Fact]
    public async Task DeleteMyAccount_rejects_a_wrong_password()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserPreferenceEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("self-delete-wrong-password");
        user.EncryptedPassword = "encrypted:Correct@123";
        await database.Insertable(user).ExecuteCommandAsync();
        var handler = new RecordingUserDeletedHandler();
        var service = CreateUserService(database, [handler], new StubSecurityManager(), new SecurityOptions());
        service.CurrentUser = new TestCurrentUser(user.Id);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.PostDeleteMyAccountAsync(new DeleteMyAccountRequest { Password = "Wrong@123" }));

        Assert.Equal(SystemErrorCodes.CurrentPasswordInvalid, exception.Code);
        Assert.NotNull(await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id));
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public async Task DeleteMyAccount_removes_the_current_account_and_notifies_handlers()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserPreferenceEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("self-delete");
        user.EncryptedPassword = "encrypted:Correct@123";
        await database.Insertable(user).ExecuteCommandAsync();
        var handler = new RecordingUserDeletedHandler();
        var service = CreateUserService(database, [handler], new StubSecurityManager(), new SecurityOptions());
        service.CurrentUser = new TestCurrentUser(user.Id);

        await service.PostDeleteMyAccountAsync(new DeleteMyAccountRequest { Password = "Correct@123" });

        var context = Assert.Single(handler.Contexts);
        Assert.Equal(user.Id, context.UserId);
        Assert.Null(await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id));
    }

    [Fact]
    public async Task DeleteMyAccount_blocks_a_builtin_account()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<UserEntity, UserRole, RoleEntity, UserPreferenceEntity, UserE2eeIdentityEntity>();
        var user = CreateUser("builtin-self-delete");
        user.EncryptedPassword = "encrypted:Correct@123";
        user.IsBuiltIn = true;
        await database.Insertable(user).ExecuteCommandAsync();
        var service = CreateUserService(database, securityManager: new StubSecurityManager(), securityOptions: new SecurityOptions());
        service.CurrentUser = new TestCurrentUser(user.Id);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.PostDeleteMyAccountAsync(new DeleteMyAccountRequest { Password = "Correct@123" }));

        Assert.Equal(SystemErrorCodes.BuiltInUserProtected, exception.Code);
        Assert.NotNull(await database.Queryable<UserEntity>().FirstAsync(candidate => candidate.Id == user.Id));
    }

    [Fact]
    public async Task System_cleanup_removes_account_scoped_rows_and_keeps_shared_ones()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<
            ExternalLoginEntity,
            RefreshTokenEntity,
            OrganizationUnitEntity,
            OrganizationMembershipEntity,
            StorageQuotaEntity>();
        database.CodeFirst.InitTables<ResourceShareLinkEntity, ResourceShareLinkViewerEntity>();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var userShareLink = CreateShareLink(userId);
        var otherShareLink = CreateShareLink(otherUserId);
        var managedUnit = CreateOrganizationUnit(managerUserId: userId);
        var otherUnit = CreateOrganizationUnit(managerUserId: otherUserId);
        await database.Insertable(CreateExternalLogin(userId)).ExecuteCommandAsync();
        await database.Insertable(CreateExternalLogin(otherUserId)).ExecuteCommandAsync();
        await database.Insertable(CreateRefreshToken(userId)).ExecuteCommandAsync();
        await database.Insertable(CreateRefreshToken(otherUserId)).ExecuteCommandAsync();
        await database.Insertable(CreateMembership(userId)).ExecuteCommandAsync();
        await database.Insertable(CreateMembership(otherUserId)).ExecuteCommandAsync();
        await database.Insertable(managedUnit).ExecuteCommandAsync();
        await database.Insertable(otherUnit).ExecuteCommandAsync();
        await database.Insertable(new StorageQuotaEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuotaBytes = 1024
        }).ExecuteCommandAsync();
        await database.Insertable(new StorageQuotaEntity
        {
            Id = Guid.NewGuid(),
            UserId = null,
            QuotaBytes = 2048
        }).ExecuteCommandAsync();
        await database.Insertable(new List<ResourceShareLinkEntity> { userShareLink, otherShareLink }).ExecuteCommandAsync();
        await database.Insertable(CreateShareLinkViewer(userShareLink.Id)).ExecuteCommandAsync();
        await database.Insertable(CreateShareLinkViewer(otherShareLink.Id)).ExecuteCommandAsync();

        var handler = new SystemUserAccountDataCleanupHandler(
            database,
            new ExternalLoginRepository(database),
            new RefreshTokenRepository(database),
            new SqlSugarRepository<OrganizationMembershipEntity>(database),
            new SqlSugarRepository<StorageQuotaEntity>(database),
            new SqlSugarRepository<ResourceShareLinkEntity>(database),
            new SqlSugarRepository<ResourceShareLinkViewerEntity>(database));

        await handler.HandleAsync(new UserDeletedContext(userId, null));

        Assert.Null(await database.Queryable<ExternalLoginEntity>().FirstAsync(row => row.UserId == userId));
        Assert.NotNull(await database.Queryable<ExternalLoginEntity>().FirstAsync(row => row.UserId == otherUserId));
        Assert.Null(await database.Queryable<RefreshTokenEntity>().FirstAsync(row => row.UserId == userId));
        Assert.NotNull(await database.Queryable<RefreshTokenEntity>().FirstAsync(row => row.UserId == otherUserId));
        Assert.Null(await database.Queryable<OrganizationMembershipEntity>().FirstAsync(row => row.UserId == userId));
        Assert.NotNull(await database.Queryable<OrganizationMembershipEntity>().FirstAsync(row => row.UserId == otherUserId));

        var refreshedManagedUnit = await database.Queryable<OrganizationUnitEntity>().FirstAsync(unit => unit.Id == managedUnit.Id);
        var refreshedOtherUnit = await database.Queryable<OrganizationUnitEntity>().FirstAsync(unit => unit.Id == otherUnit.Id);
        Assert.Null(refreshedManagedUnit.ManagerUserId);
        Assert.Equal(otherUserId, refreshedOtherUnit.ManagerUserId);

        var quotas = await database.Queryable<StorageQuotaEntity>().ToListAsync();
        Assert.Single(quotas);
        Assert.Null(quotas[0].UserId);
        Assert.Equal(2048, quotas[0].QuotaBytes);

        Assert.Null(await database.Queryable<ResourceShareLinkEntity>().FirstAsync(link => link.OwnerId == userId));
        Assert.NotNull(await database.Queryable<ResourceShareLinkEntity>().FirstAsync(link => link.OwnerId == otherUserId));
        Assert.Null(await database.Queryable<ResourceShareLinkViewerEntity>()
            .FirstAsync(viewer => viewer.ShareLinkId == userShareLink.Id));
        Assert.NotNull(await database.Queryable<ResourceShareLinkViewerEntity>()
            .FirstAsync(viewer => viewer.ShareLinkId == otherShareLink.Id));
    }


    private static UserService CreateUserService(
        ISqlSugarClient database,
        IEnumerable<IUserDeletedHandler>? userDeletedHandlers = null,
        ISecurityManager? securityManager = null,
        SecurityOptions? securityOptions = null)
    {
        return new UserService(
            securityManager ?? null!,
            new UserRepository(database),
            new UserRoleRepository(database),
            null!,
            null!,
            null!,
            new RoleRepository(database),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            null!,
            securityOptions is null ? null! : Options.Create(securityOptions),
            null!,
            null!,
            null!,
            null!,
            null!,
            new RecordingSessionRevoker(),
            null!,
            null!,
            new SqlSugarRepository<UserPreferenceEntity>(database),
            new SqlSugarRepository<UserE2eeIdentityEntity>(database),
            new ConfigurationBuilder().Build(),
            NullLogger<UserService>.Instance,
            null,
            null,
            null,
            userDeletedHandlers)
        {
            ObjectMapper = new MapsterObjectMapper()
        };
    }

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"user-deletion-cleanup-tests-{Guid.NewGuid():N}",
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


    private static ExternalLoginEntity CreateExternalLogin(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Provider = "microsoft",
        ProviderKey = Guid.NewGuid().ToString("N"),
        CreationTime = DateTime.UtcNow
    };

    private static RefreshTokenEntity CreateRefreshToken(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FamilyId = Guid.NewGuid(),
        Token = Guid.NewGuid().ToString("N"),
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        IsRevoked = false,
        CreationTime = DateTime.UtcNow
    };

    private static OrganizationUnitEntity CreateOrganizationUnit(Guid? managerUserId) => new()
    {
        Id = Guid.NewGuid(),
        Code = Guid.NewGuid().ToString("N"),
        Name = "组织",
        Path = "/org",
        Depth = 0,
        ManagerUserId = managerUserId,
        IsActive = true,
        CreationTime = DateTime.UtcNow
    };

    private static OrganizationMembershipEntity CreateMembership(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationUnitId = Guid.NewGuid(),
        UserId = userId,
        MembershipType = "Primary",
        CreationTime = DateTime.UtcNow
    };

    private static ResourceShareLinkEntity CreateShareLink(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        TokenVersion = 1,
        ResourceType = "system.file",
        ResourceId = Guid.NewGuid(),
        OwnerId = ownerId,
        TokenHash = Guid.NewGuid().ToString("N"),
        ExpiresAt = DateTime.UtcNow.AddDays(1),
        CreationTime = DateTime.UtcNow
    };

    private static ResourceShareLinkViewerEntity CreateShareLinkViewer(Guid shareLinkId) => new()
    {
        Id = Guid.NewGuid(),
        ShareLinkId = shareLinkId,
        FingerprintHash = new string('b', 64),
        FirstAccessedAt = DateTime.UtcNow,
        LastAccessedAt = DateTime.UtcNow,
        CreationTime = DateTime.UtcNow
    };

    private sealed class RecordingUserDeletedHandler : IUserDeletedHandler
    {
        public List<UserDeletedContext> Contexts { get; } = [];

        public Task HandleAsync(UserDeletedContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSessionRevoker : IUserSessionRevoker
    {
        public Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds) => Task.CompletedTask;

        public Task<int> RevokeSessionAsync(Guid userId, Guid sessionId) => Task.FromResult(1);
    }

    private sealed class StubSecurityManager : ISecurityManager
    {
        public (bool IsValid, List<string> Errors) ValidatePassword(string password) => (true, []);

        public (string EncryptedPassword, string Salt) EncryptPassword(string password) =>
            ($"encrypted:{password}", "test-salt");

        public bool VerifyPassword(UserEntity user, string password) =>
            user.EncryptedPassword == $"encrypted:{password}";

        public (string PublicKey, string PrivateKey) GenerateSm2KeyPair() => (string.Empty, string.Empty);

        public string GenerateAccessToken(IEnumerable<Claim> claims) => string.Empty;

        public string GenerateRefreshToken() => string.Empty;

        public string GenerateSm4Key() => string.Empty;
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "self-delete-tester";
        public string? Name => "Self";
        public string? SurName => "Delete";
        public string? Email => "self-delete-tester@example.test";
        public bool EmailVerified => true;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => ["user"];
        public Claim? FindClaim(string claimType) => null;
        public string? FindClaimValue(string claimType) => null;
        public Claim[] FindClaims(string claimType) => [];
        public Claim[] GetAllClaims() => [];
        public bool IsInRole(string roleName) => roleName == "user";
    }

}
