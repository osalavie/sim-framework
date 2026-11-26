using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.Admin.Domain.Data;
using Sim.Approval.Application.Seeds;
using Sim.System.SqlSugar.Repositories;
using SqlSugar;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Role;
using Sim.System.SqlSugar.Repositories.ModuleConfig;
using Sim.System.SqlSugar.Repositories.Role;

namespace Sim.Security.Tests;

/// <summary>
/// 普通用户角色基础模块种子：只给 User 角色追加端用户模块，
/// 内置角色不受影响，重复执行保持幂等。
/// </summary>
public sealed class UserRoleAssignmentDataSeederTests
{
    static UserRoleAssignmentDataSeederTests()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    [Fact]
    public async Task SeedAsync_assigns_basic_modules_to_user_role_only()
    {
        using var database = CreateDatabase();
        database.CodeFirst.InitTables<
            RoleEntity,
            ModuleConfigEntity,
            RoleModule>();

        var userRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "User",
            Code = "User"
        };
        var adminRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Code = "Admin",
            IsBuiltIn = true
        };
        var approvalModule = CreateModule("approval");
        var systemModule = CreateModule("system-management");

        await database.Insertable(new[] { userRole, adminRole }).ExecuteCommandAsync();
        await database.Insertable(new[] { approvalModule, systemModule }).ExecuteCommandAsync();

        var seeder = CreateSeeder(database);
        await seeder.SeedAsync();

        var userModuleLinks = await database.Queryable<RoleModule>()
            .Where(link => link.RoleId == userRole.Id)
            .ToListAsync();
        Assert.Single(userModuleLinks);
        Assert.Contains(userModuleLinks, link => link.ModuleId == approvalModule.Id);
        Assert.DoesNotContain(userModuleLinks, link => link.ModuleId == systemModule.Id);

        var adminModuleCount = await database.Queryable<RoleModule>()
            .Where(link => link.RoleId == adminRole.Id)
            .CountAsync();
        Assert.Equal(0, adminModuleCount);

        await seeder.SeedAsync();
        Assert.Equal(
            1,
            await database.Queryable<RoleModule>().Where(link => link.RoleId == userRole.Id).CountAsync());
    }

    private static UserRoleAssignmentDataSeeder CreateSeeder(ISqlSugarClient database) =>
        new(
            new RoleRepository(database),
            new ModuleConfigRepository(database, NullLogger<ModuleConfigRepository>.Instance),
            new RoleModuleRepository(database),
            [new ApprovalModuleSeedContributor(null!, NullLogger<ApprovalModuleSeedContributor>.Instance)],
            NullLogger<UserRoleAssignmentDataSeeder>.Instance);

    private static ModuleConfigEntity CreateModule(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = $"{name} module",
        Enabled = true,
        Order = 1
    };

    private static SqlSugarScope CreateDatabase() => new(new ConnectionConfig
    {
        ConfigId = $"user-role-assignment-tests-{Guid.NewGuid():N}",
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
}
