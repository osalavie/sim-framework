using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;

namespace Sim.Approval.SqlSugar.Repositories;

public class ApprovalInstanceRepository : SqlSugarRepository<ApprovalInstance>, IApprovalInstanceRepository
{
    public ApprovalInstanceRepository(ISqlSugarClient db) : base(db)
    {
    }

    public Task<int> TryAdvanceAsync(ApprovalInstance instance, int expectedVersion, Guid? modifierUserId)
    {
        // 条件更新（WHERE Version = @expected）替代隐式乐观锁：并发推进时只有一个调用能成功。
        return AsTenant().UpdateableWithAttr<ApprovalInstance>()
            .SetColumns(item => item.Title == instance.Title)
            .SetColumns(item => item.ContextJson == instance.ContextJson)
            .SetColumns(item => item.Status == instance.Status)
            .SetColumns(item => item.CompletedTime == instance.CompletedTime)
            .SetColumns(item => item.RunningKey == instance.RunningKey)
            .SetColumns(item => item.CurrentNodeSort == instance.CurrentNodeSort)
            .SetColumns(item => item.CurrentNodeKey == instance.CurrentNodeKey)
            .SetColumns(item => item.CurrentNodeName == instance.CurrentNodeName)
            .SetColumns(item => item.Version == instance.Version)
            .SetColumns(item => item.LastModificationTime == DateTime.Now)
            .SetColumns(item => item.LastModifierId == modifierUserId)
            .Where(item => item.Id == instance.Id && item.Version == expectedVersion)
            .ExecuteCommandAsync();
    }
}

public class ApprovalTaskRepository : SqlSugarRepository<ApprovalTask>, IApprovalTaskRepository
{
    public ApprovalTaskRepository(ISqlSugarClient db) : base(db)
    {
    }

    public Task<int> TryMarkTimeoutNotifiedAsync(Guid taskId, DateTime notifiedAt) =>
        AsTenant().UpdateableWithAttr<ApprovalTask>()
            .SetColumns(item => item.TimeoutNotifiedAt == notifiedAt)
            .Where(item => item.Id == taskId
                           && item.Status == ApprovalTaskStatus.Pending
                           && item.TimeoutNotifiedAt == null)
            .ExecuteCommandAsync();

    public Task<int> TryFinalizeAsync(
        Guid taskId,
        ApprovalTaskStatus expectedStatus,
        ApprovalTaskStatus targetStatus,
        string? comment,
        DateTime handledTime,
        Guid? modifierUserId)
    {
        return AsTenant().UpdateableWithAttr<ApprovalTask>()
            .SetColumns(item => item.Status == targetStatus)
            .SetColumns(item => item.Comment == comment)
            .SetColumns(item => item.HandledTime == handledTime)
            .SetColumns(item => item.LastModificationTime == DateTime.Now)
            .SetColumns(item => item.LastModifierId == modifierUserId)
            .Where(item => item.Id == taskId && item.Status == expectedStatus)
            .ExecuteCommandAsync();
    }
}

public class ApprovalRecordRepository : SqlSugarRepository<ApprovalRecord>, IApprovalRecordRepository
{
    public ApprovalRecordRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ApprovalCcRepository : SqlSugarRepository<ApprovalCc>, IApprovalCcRepository
{
    public ApprovalCcRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ApprovalInstanceNodeRepository
    : SqlSugarRepository<ApprovalInstanceNode>, IApprovalInstanceNodeRepository
{
    public ApprovalInstanceNodeRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ApprovalAttachmentRepository
    : SqlSugarRepository<ApprovalAttachment>, IApprovalAttachmentRepository
{
    public ApprovalAttachmentRepository(ISqlSugarClient db) : base(db)
    {
    }
}
