namespace Sim.Approval.Domain.Shared;

/// <summary>审批模块常量：业务编号键、通知事件名、内置定时任务与字段长度约束。</summary>
public static class ApprovalConsts
{
    /// <summary>审批实例业务编号键（对应编号管理中的定义记录）。</summary>
    public const string InstanceNumberBusinessKey = "approval.instance";

    /// <summary>审批实例编号默认格式。</summary>
    public const string InstanceNumberFormat = "SP-{date:yyyyMMdd}-{sequence:D5}";

    /// <summary>待办生成通知事件名。</summary>
    public const string NotificationEventTaskCreated = "approval.task.created";

    /// <summary>实例结束通知事件名。</summary>
    public const string NotificationEventInstanceFinished = "approval.instance.finished";

    /// <summary>催办通知事件名。</summary>
    public const string NotificationEventInstanceUrged = "approval.instance.urged";

    /// <summary>超时提醒通知事件名。</summary>
    public const string NotificationEventTaskTimeout = "approval.task.timeout";

    /// <summary>抄送通知事件名。</summary>
    public const string NotificationEventCcCreated = "approval.cc.created";

    /// <summary>超时扫描内置任务分组。</summary>
    public const string TimeoutScanTaskGroup = "Approval";

    /// <summary>超时扫描内置任务键。</summary>
    public const string TimeoutScanTaskKey = "ApprovalTimeoutScanJob";

    /// <summary>超时扫描任务默认 Cron（每 5 分钟）。</summary>
    public const string TimeoutScanTaskCron = "0 0/5 * * * ?";

    /// <summary>催办频控间隔（小时）：同一实例同一操作人在该间隔内只能催办一次。</summary>
    public const int UrgeIntervalHours = 1;

    /// <summary>审批实例在途唯一索引名（(TenantScope, RunningKey)），用于识别唯一键冲突。</summary>
    public const string RunningKeyUniqueIndex = "uk_approval_instance_running_key";

    /// <summary>单条处理意见最大长度。</summary>
    public const int MaxCommentLength = 1000;

    /// <summary>实例标题最大长度。</summary>
    public const int MaxTitleLength = 256;

    /// <summary>业务类型最大长度。</summary>
    public const int MaxBusinessTypeLength = 128;

    /// <summary>业务对象标识最大长度。</summary>
    public const int MaxBusinessIdLength = 128;

    /// <summary>节点标识最大长度。</summary>
    public const int MaxNodeKeyLength = 64;

    /// <summary>流程编码最大长度。</summary>
    public const int MaxFlowCodeLength = 128;

    /// <summary>流程名称最大长度。</summary>
    public const int MaxFlowNameLength = 256;

    /// <summary>节点名称最大长度。</summary>
    public const int MaxNodeNameLength = 128;

    /// <summary>条件表达式 JSON 最大长度。</summary>
    public const int MaxConditionJsonLength = 4000;

    /// <summary>抄送配置 JSON 最大长度。</summary>
    public const int MaxCcConfigJsonLength = 2000;

    /// <summary>审批人列表 JSON 最大长度。</summary>
    public const int MaxApproverJsonLength = 4000;

    /// <summary>画布布局 JSON 最大长度（坐标按节点标识存储，引擎不读取）。</summary>
    public const int MaxLayoutJsonLength = 8000;

    /// <summary>连线标签最大长度。</summary>
    public const int MaxEdgeLabelLength = 64;

    /// <summary>业务字段标识最大长度。</summary>
    public const int MaxFieldKeyLength = 64;

    /// <summary>业务字段显示名最大长度。</summary>
    public const int MaxFieldDisplayNameLength = 128;

    /// <summary>业务字段取值最大长度。</summary>
    public const int MaxFieldValueLength = 2000;

    /// <summary>业务字段输入提示最大长度。</summary>
    public const int MaxFieldPlaceholderLength = 256;

    /// <summary>附件文件名最大长度（超出部分截断保存）。</summary>
    public const int MaxAttachmentFileNameLength = 255;

    /// <summary>线性定义合成图时使用的开始节点标识（内存图中的合成边界，实例创建时会落库为节点行）。</summary>
    public const string SynthesizedStartNodeKey = "__start";

    /// <summary>线性定义合成图时使用的结束节点标识（内存图中的合成边界，实例创建时会落库为节点行）。</summary>
    public const string SynthesizedEndNodeKey = "__end";
}
