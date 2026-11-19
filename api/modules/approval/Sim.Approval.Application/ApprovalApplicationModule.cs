using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sim.Approval.Application.Contracts;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Application.Jobs;
using Sim.Approval.Application.Seeds;
using Sim.Approval.Application.Services;
using Sim.Approval.SqlSugar;
using Sim.System.Application.Contracts;
using Sim.System.Application.Contracts.ScheduleTask;
using Volo.Abp.Modularity;

namespace Sim.Approval.Application;

[DependsOn(
    typeof(ApprovalApplicationContractsModule),
    typeof(ApprovalSqlSugarModule),
    typeof(SystemApplicationContractsModule))]
public class ApprovalApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 应用服务由 AppServiceFactory 依约定创建，此处注册运行时服务与跨模块端口
        context.Services.AddScoped<ApprovalBusinessRegistry>();
        context.Services.AddScoped<ApprovalApproverResolver>();
        context.Services.AddScoped<ApprovalNotificationDispatcher>();
        context.Services.AddScoped<ApprovalEngine>();
        context.Services.AddScoped<ApprovalQueryService>();
        context.Services.AddScoped<ApprovalAttachmentService>();
        context.Services.AddScoped<IApprovalSubmitService, ApprovalSubmitService>();
        context.Services.AddScoped<IApprovalStatusQuery, ApprovalStatusQuery>();

        // 内建通用业务类型，保证框架自带的审批中心开箱可发起；业务模块追加注册各自类型。
        context.Services.AddTransient<IApprovalBusinessDescriptor, GenericApprovalBusinessDescriptor>();

        context.Services.AddTransient<ApprovalTimeoutScanJob>();
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<
            IScheduleTaskDefinitionProvider,
            ApprovalTimeoutScanTaskProvider>());

        // 审批模块自描述种子：模块、权限、路由与编号定义，由平台统一汇总执行。
        context.Services.AddTransient<ApprovalModuleSeedContributor>();
        context.Services.AddTransient<Sim.Framework.Domain.Shared.Data.IModuleSeedContributor>(
            provider => provider.GetRequiredService<ApprovalModuleSeedContributor>());
        context.Services.AddTransient<Sim.Framework.Domain.Shared.Data.IDataSeedContributor>(
            provider => provider.GetRequiredService<ApprovalModuleSeedContributor>());
    }
}
