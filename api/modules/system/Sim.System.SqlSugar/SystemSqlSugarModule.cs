using Microsoft.Extensions.DependencyInjection;
using Sim.SqlSugar.Core.Migrations;
using Sim.System.Domain;
using Sim.System.SqlSugar.Migrations;
using Volo.Abp.Modularity;

namespace Sim.System.SqlSugar;

[DependsOn(
    typeof(SystemDomainModule)
)]
public class SystemSqlSugarModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IMigration, OrganizationUnitParentCodeScopeMigration>();
        context.Services.AddTransient<IMigration, RouteConfigModuleOwnershipRemovalMigration>();
        context.Services.AddTransient<IMigration, RouteNameNormalizationMigration>();
        context.Services.AddTransient<IMigration, UserNickNameUniqueMigration>();
        context.Services.AddTransient<IMigration, BuiltInIdentityFlagsMigration>();
        context.Services.AddTransient<IMigration, TenantRoleBuiltInIsolationMigration>();
        context.Services.AddTransient<IMigration, HostBuiltInIdentityBackfillMigration>();
        context.Services.AddTransient<IMigration, HostAdministratorBuiltInMigration>();
    }

    // System 仓储通过 ISqlSugarRepository 子接口约定自动注册（I{Name}Repository → {Name}Repository）。
    // 包括：IUserRepository, IUserRoleRepository, IRoleRepository, IRolePermissionRepository,
    //       IPermissionRepository, IRefreshTokenRepository, IRoleModuleRepository,
    //       IModuleConfigRepository, IRouteConfigRepository, IRouteMetaRepository
}
