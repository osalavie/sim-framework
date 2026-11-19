using Microsoft.Extensions.DependencyInjection;
using Sim.Admin.Domain.Data;
using Sim.Admin.Domain.Shared;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain;
using Volo.Abp.Modularity;

namespace Sim.Admin.Domain;


[DependsOn(
    typeof(AdminDomainSharedModule),
    typeof(SystemDomainModule)
    )]
public class AdminDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 数据种子通过标准 DI 注册（原 ABP ITransientDependency 自动注册）
        context.Services.AddTransient<TenantDataSeeder>();
        context.Services.AddTransient<SystemDataSeeder>();
        context.Services.AddTransient<RoleDataSeeder>();
        context.Services.AddTransient<UserDataSeeder>();
        context.Services.AddTransient<SystemUserDataSeeder>();
        context.Services.AddTransient<ModuleDataSeeder>();
        context.Services.AddTransient<RouteDataSeeder>();
        context.Services.AddTransient<PermissionDataSeeder>();
        context.Services.AddTransient<SystemConfigurationDataSeeder>();
        context.Services.AddTransient<OrganizationDataSeeder>();
        context.Services.AddTransient<AdminRoleAssignmentDataSeeder>();
        context.Services.AddTransient<UserRoleAssignmentDataSeeder>();
        context.Services.AddTransient<SetupStateReader>();
        context.Services.AddTransient<IDataSeedContributor, DataSeederContributor>();

        // 租户配置初始化服务：实现在 Admin.Domain（可访问 System 模块实体），
        // 通过 ITenantProvisioningService 接口注入到 TenantAppService。
        context.Services.AddTransient<Sim.Admin.Domain.Services.TenantProvisioningService>();
        context.Services.AddTransient<Sim.System.Application.Contracts.Tenant.ITenantProvisioningService>(
            sp => sp.GetRequiredService<Sim.Admin.Domain.Services.TenantProvisioningService>());

        context.Services.AddTransient<Sim.Admin.Domain.Services.OpenPlatformTokenIssuer>();
        context.Services.AddTransient<Sim.OpenPlatform.Application.Contracts.IOpenPlatformTokenIssuer>(
            sp => sp.GetRequiredService<Sim.Admin.Domain.Services.OpenPlatformTokenIssuer>());
        context.Services.AddTransient<Sim.Admin.Domain.Services.OpenPlatformScopeCatalog>();
        context.Services.AddTransient<Sim.OpenPlatform.Application.Contracts.IOpenPlatformScopeCatalog>(
            sp => sp.GetRequiredService<Sim.Admin.Domain.Services.OpenPlatformScopeCatalog>());

        // 租户解析由 Host 层的 CurrentTenant（ICurrentTenant）承担，
        // 通过 TenantContextSyncMiddleware 同步到 SqlSugar 查询过滤器。
    }
}

