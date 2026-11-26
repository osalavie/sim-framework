namespace Sim.Approval.Domain.Shared;

/// <summary>
/// 审批模块权限码。与 <c>PermissionCatalog</c> 登记保持一致，由数据种子幂等写入数据库。
/// </summary>
public static class ApprovalPermissions
{
    /// <summary>审批流程定义管理（创建、编辑、发布、停用、删除草稿）。</summary>
    public const string ManageDefinition = "approval:definition:manage";

    /// <summary>审批实例监控（跨用户查看全部实例与处理记录）。</summary>
    public const string MonitorInstance = "approval:instance:monitor";

    /// <summary>终止审批实例（强制结束进行中的实例）。</summary>
    public const string TerminateInstance = "approval:instance:terminate";
}
