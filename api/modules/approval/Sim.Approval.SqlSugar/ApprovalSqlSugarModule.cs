using Microsoft.Extensions.DependencyInjection;
using Sim.Approval.Domain;
using Sim.Approval.SqlSugar.Migrations;
using Sim.SqlSugar.Core.Migrations;
using Volo.Abp.Modularity;

namespace Sim.Approval.SqlSugar;

[DependsOn(typeof(ApprovalDomainModule))]
public class ApprovalSqlSugarModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 审批仓储实现通过 ISqlSugarRepository 子接口约定自动注册（I{Name}Repository → {Name}Repository）。
        context.Services.AddTransient<IMigration, ApprovalDefinitionTenantScopeMigration>();
    }
}
