using Sim.Framework.Application.Mapping;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Role;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.SqlSugar.Repositories;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Application.Role;
using Sim.System.Application.User;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;
using Sim.System.SqlSugar.Repositories.Permission;
using Sim.System.SqlSugar.Repositories.Role;
using Sim.System.SqlSugar.Repositories.User;

namespace Sim.Security.Tests;

public sealed class RoleAssignmentTests
{
    static RoleAssignmentTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task AssignRolePermissionsAsync_rejects_unknown_permission()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, PermissionEntity, RolePermission, UserRole, RoleModule>();
        var role = CreateRole("Editor");
        await database.Insertable(role).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRolePermissionsAsync(role.Id, [Guid.NewGuid()]));

        Assert.Equal(SystemErrorCodes.PermissionNotFound, exception.Code);
        var links = await database.Queryable<RolePermission>()
            .Where(link => link.RoleId == role.Id)
            .ToListAsync();
        Assert.Empty(links);
    }

    [Fact]
    public async Task AssignRolePermissionsAsync_replaces_links_and_revokes_role_users()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, PermissionEntity, RolePermission, UserRole, RoleModule>();
        var role = CreateRole("Editor");
        var permission = CreatePermission("system:user:query");
        var userId = Guid.NewGuid();
        await database.Insertable(role).ExecuteCommandAsync();
        await database.Insertable(permission).ExecuteCommandAsync();
        await database.Insertable(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id })
            .ExecuteCommandAsync();
        var service = CreateService(database, out var revoker);

        await service.AssignRolePermissionsAsync(role.Id, [permission.Id]);

        var links = await database.Queryable<RolePermission>()
            .Where(link => link.RoleId == role.Id)
            .ToListAsync();
        Assert.Single(links);
        Assert.Equal(permission.Id, links[0].PermissionId);
        Assert.Contains(userId, revoker.Revoked);
    }

    [Fact]
    public async Task AssignRolePermissionsAsync_skips_revocation_when_unchanged()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, PermissionEntity, RolePermission, UserRole, RoleModule>();
        var role = CreateRole("Editor");
        var permission = CreatePermission("system:user:query");
        var userId = Guid.NewGuid();
        await database.Insertable(role).ExecuteCommandAsync();
        await database.Insertable(permission).ExecuteCommandAsync();
        await database.Insertable(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id })
            .ExecuteCommandAsync();
        await database.Insertable(new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = permission.Id
        }).ExecuteCommandAsync();
        var service = CreateService(database, out var revoker);

        await service.AssignRolePermissionsAsync(role.Id, [permission.Id]);

        Assert.Empty(revoker.Revoked);
    }

    [Fact]
    public async Task UpdateAsync_allows_name_and_description_changes_for_builtin_role()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, RolePermission, UserRole, RoleModule>();
        var adminRole = CreateRole("Admin", isBuiltIn: true);
        await database.Insertable(adminRole).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var updated = await service.UpdateAsync(new UpdateRoleDto
        {
            Id = adminRole.Id,
            Code = "Admin",
            Name = "Renamed admin",
            Description = "Updated"
        });

        Assert.Equal("Renamed admin", updated.Name);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_blocks_code_change_for_builtin_role()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, RolePermission, UserRole, RoleModule>();
        var adminRole = CreateRole("Admin", isBuiltIn: true);
        await database.Insertable(adminRole).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(new UpdateRoleDto
            {
                Id = adminRole.Id,
                Code = "SuperAdmin",
                Name = "Renamed admin"
            }));

        Assert.Equal(SystemErrorCodes.BuiltInRoleProtected, exception.Code);
    }

    [Fact]
    public async Task AssignRolePermissionsAsync_blocks_builtin_admin_role()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, PermissionEntity, RolePermission, UserRole, RoleModule>();
        var adminRole = CreateRole("Admin", isBuiltIn: true);
        await database.Insertable(adminRole).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AssignRolePermissionsAsync(adminRole.Id, []));

        Assert.Equal(SystemErrorCodes.BuiltInRoleProtected, exception.Code);
    }

    [Fact]
    public async Task DeleteAsync_blocks_builtin_admin_role()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, RolePermission, UserRole, RoleModule, RoleDataScopeEntity>();
        var adminRole = CreateRole("Admin", isBuiltIn: true);
        await database.Insertable(adminRole).ExecuteCommandAsync();
        var service = CreateService(database, out _);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.DeleteAsync(adminRole.Id));

        Assert.Equal(SystemErrorCodes.BuiltInRoleProtected, exception.Code);
    }

    [Fact]
    public async Task DeleteAsync_cleans_role_links_and_revokes_role_users()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<RoleEntity, PermissionEntity, RolePermission, UserRole, RoleModule>();
        database.CodeFirst.InitTables<RoleDataScopeEntity>();
        var role = CreateRole("Editor");
        var permission = CreatePermission("system:user:query");
        var userId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        await database.Insertable(role).ExecuteCommandAsync();
        await database.Insertable(permission).ExecuteCommandAsync();
        await database.Insertable(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id })
            .ExecuteCommandAsync();
        await database.Insertable(new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = permission.Id
        }).ExecuteCommandAsync();
        await database.Insertable(new RoleModule
        {
            RoleId = role.Id,
            ModuleId = moduleId
        }).ExecuteCommandAsync();
        var service = CreateService(database, out var revoker);

        var deleted = await service.DeleteAsync(role.Id);

        Assert.True(deleted);
        Assert.Empty(await database.Queryable<RolePermission>().Where(link => link.RoleId == role.Id).ToListAsync());
        Assert.Empty(await database.Queryable<UserRole>().Where(link => link.RoleId == role.Id).ToListAsync());
        Assert.Empty(await database.Queryable<RoleModule>().Where(link => link.RoleId == role.Id).ToListAsync());
        Assert.Contains(userId, revoker.Revoked);
    }

    private static RoleService CreateService(ISqlSugarClient database, out RecordingSessionRevoker revoker)
    {
        revoker = new RecordingSessionRevoker();
        return new RoleService(
            new RoleRepository(database),
            new RolePermissionRepository(database),
            new PermissionRepository(database),
            new UserRoleRepository(database),
            new RoleModuleRepository(database),
            new SqlSugarRepository<RoleDataScopeEntity>(database),
            revoker)
        {
            ObjectMapper = new MapsterObjectMapper()
        };
    }

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"role-assignment-tests-{Guid.NewGuid():N}",
        DbType = DbType.Sqlite,
        ConnectionString = "DataSource=:memory:",
        IsAutoCloseConnection = false,
        ConfigureExternalServices = new ConfigureExternalServices
        {
            EntityService = (property, column) =>
            {
                if (property.Name == "Id") column.IsPrimarykey = true;
                if (property.Name == "IdType") column.IsIgnore = true;
                if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                {
                    column.IsNullable = true;
                }
            }
        }
    });

    private static RoleEntity CreateRole(string code, bool isBuiltIn = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = code,
        Code = code,
        Description = string.Empty,
        IsBuiltIn = isBuiltIn,
        CreationTime = DateTime.UtcNow
    };

    private static PermissionEntity CreatePermission(string code) => new()
    {
        Id = Guid.NewGuid(),
        Name = code,
        Code = code,
        Description = string.Empty,
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
