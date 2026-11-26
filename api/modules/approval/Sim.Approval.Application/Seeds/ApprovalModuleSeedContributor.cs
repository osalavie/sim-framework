using Microsoft.Extensions.Logging;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Application.Contracts.Numbering;

namespace Sim.Approval.Application.Seeds;

/// <summary>
/// 审批模块的种子自描述：功能模块、权限、路由与实例编号定义。
/// 平台只负责汇总执行，不感知审批模块的具体内容。
/// </summary>
public sealed class ApprovalModuleSeedContributor(
    IBusinessNumberDefinitionSeeder numberDefinitionSeeder,
    ILogger<ApprovalModuleSeedContributor> logger)
    : IModuleSeedContributor, IDataSeedContributor
{
    private static readonly DataSeedStep NumberingStep =
        new("approval-numbering", "创建审批编号定义");

    private const string MenuApproval = "Menu:Approval";
    private const string MenuApprovalTodo = "Menu:ApprovalTodo";
    private const string MenuApprovalHandled = "Menu:ApprovalHandled";
    private const string MenuApprovalSubmitted = "Menu:ApprovalSubmitted";
    private const string MenuApprovalCc = "Menu:ApprovalCc";
    private const string MenuApprovalDefinition = "Menu:ApprovalDefinition";
    private const string MenuApprovalMonitor = "Menu:ApprovalMonitor";

    public IReadOnlyList<ModuleSeed> Modules => ModuleSeeds;

    public IReadOnlyList<PermissionSeed> Permissions => PermissionSeeds;

    public IReadOnlyList<RouteSeed> Routes => RouteSeeds;

    public static IReadOnlyList<ModuleSeed> ModuleSeeds { get; } =
    [
        new(
            Name: "approval",
            Description: "审批模块，包含审批中心、流程定义管理与实例监控",
            Icon: "carbon:Flow",
            Order: 4000,
            Layout: "admin",
            EmptyRedirect: "/admin/approval/todo",
            DefaultForUserRole: true)
    ];

    public static IReadOnlyList<PermissionSeed> PermissionSeeds { get; } =
    [
        new(ApprovalPermissions.ManageDefinition, "审批流程定义", "创建、编辑、发布、停用与删除审批流程草稿"),
        new(ApprovalPermissions.MonitorInstance, "审批实例监控", "跨用户查看全部审批实例与处理记录"),
        new(ApprovalPermissions.TerminateInstance, "终止审批实例", "强制结束进行中的审批实例")
    ];

    public static IReadOnlyList<RouteSeed> RouteSeeds { get; } =
    [
        new(
            ModuleName: "approval",
            Name: "Approval",
            Path: "approval",
            Component: "Layout",
            ParentName: null,
            Title: MenuApproval,
            Icon: "carbon:Flow",
            Order: 4000,
            Menu: true,
            Tab: true,
            KeepAlive: true,
            Redirect: "/admin/approval/todo"),
        new("approval", "ApprovalTodo", "todo", "approval/views/todo/index", "Approval",
            MenuApprovalTodo, "carbon:Task", 1, true, true, true),
        new("approval", "ApprovalHandled", "handled", "approval/views/handled/index", "Approval",
            MenuApprovalHandled, "carbon:CheckmarkOutline", 2, true, true, true),
        new("approval", "ApprovalSubmitted", "submitted", "approval/views/submitted/index", "Approval",
            MenuApprovalSubmitted, "carbon:SendAlt", 3, true, true, true),
        new("approval", "ApprovalCc", "cc", "approval/views/cc/index", "Approval",
            MenuApprovalCc, "carbon:SendToBack", 4, true, true, true),
        new("approval", "ApprovalDefinition", "definition", "approval/views/definition/index", "Approval",
            MenuApprovalDefinition, "carbon:FlowData", 5, true, true, true,
            ApprovalPermissions.ManageDefinition),
        new("approval", "ApprovalMonitor", "monitor", "approval/views/monitor/index", "Approval",
            MenuApprovalMonitor, "carbon:ChartLineData", 6, true, true, true,
            ApprovalPermissions.MonitorInstance),
        new("approval", "ApprovalInstanceDetail", "detail/:instanceId", "approval/views/detail/index", "Approval",
            MenuApprovalTodo, "carbon:Document", 7, false, true, false),
        new("approval", "ApprovalDefinitionEdit", "definition/edit/:definitionId?", "approval/views/definition/edit",
            "Approval", MenuApprovalDefinition, "carbon:Edit", 8, false, true, false,
            ApprovalPermissions.ManageDefinition)
    ];

    public IReadOnlyList<DataSeedStep> Steps { get; } = [NumberingStep];

    public async Task SeedAsync(Action<DataSeedStep>? onStep = null)
    {
        onStep?.Invoke(NumberingStep);
        await numberDefinitionSeeder.EnsureAsync(
            tenantScope: "host",
            businessKey: ApprovalConsts.InstanceNumberBusinessKey,
            displayName: "审批实例编号",
            description: "审批实例发起时按 `SP-日期-序号` 生成",
            format: ApprovalConsts.InstanceNumberFormat,
            initialSequence: 1);
        logger.LogInformation("审批实例编号定义已就绪: {BusinessKey}", ApprovalConsts.InstanceNumberBusinessKey);
    }
}
