using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.Approval.Domain.IRepositories;

public interface IApprovalInstanceRepository : ISqlSugarRepository<ApprovalInstance>
{
    /// <summary>
    /// 原子推进实例状态：仅当库中 <c>Version</c> 等于 <paramref name="expectedVersion"/> 时写入状态列，
    /// 写入值取自实体（含 <see cref="ApprovalInstance.Version"/>，调用方先递增）。
    /// </summary>
    /// <param name="instance">已变更状态的实例（其 Version 为写入后的新值）。</param>
    /// <param name="expectedVersion">期望的库中版本号（即变更前的版本）。</param>
    /// <param name="modifierUserId">操作人，用于写入修改审计。</param>
    /// <returns>受影响行数（0 表示版本已被其他操作推进）。</returns>
    Task<int> TryAdvanceAsync(ApprovalInstance instance, int expectedVersion, Guid? modifierUserId);
}

public interface IApprovalTaskRepository : ISqlSugarRepository<ApprovalTask>
{
    /// <summary>仅当任务仍待办且从未发送超时提醒时，原子标记本次提醒。</summary>
    Task<int> TryMarkTimeoutNotifiedAsync(Guid taskId, DateTime notifiedAt);

    /// <summary>
    /// 原子定案任务：仅当库中状态仍为 <paramref name="expectedStatus"/> 时写入结论。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="expectedStatus">期望的当前状态。</param>
    /// <param name="targetStatus">目标状态。</param>
    /// <param name="comment">处理意见。</param>
    /// <param name="handledTime">处理时间。</param>
    /// <param name="modifierUserId">操作人，用于写入修改审计。</param>
    /// <returns>受影响行数（0 表示任务已被他人定案）。</returns>
    Task<int> TryFinalizeAsync(
        Guid taskId,
        ApprovalTaskStatus expectedStatus,
        ApprovalTaskStatus targetStatus,
        string? comment,
        DateTime handledTime,
        Guid? modifierUserId);
}

public interface IApprovalRecordRepository : ISqlSugarRepository<ApprovalRecord>
{
}

public interface IApprovalCcRepository : ISqlSugarRepository<ApprovalCc>
{
}

public interface IApprovalInstanceNodeRepository : ISqlSugarRepository<ApprovalInstanceNode>
{
}

public interface IApprovalAttachmentRepository : ISqlSugarRepository<ApprovalAttachment>
{
}
