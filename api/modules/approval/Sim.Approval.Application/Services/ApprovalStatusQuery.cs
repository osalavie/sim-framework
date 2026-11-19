using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Shared;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批状态查询端口实现 —— 按业务对象返回最近一次实例快照，供业务列表角标与详情展示。
/// </summary>
public sealed class ApprovalStatusQuery(
    IApprovalInstanceRepository instanceRepository) : IApprovalStatusQuery
{
    public async Task<ApprovalStatusSnapshot?> FindAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var type = (businessType ?? string.Empty).Trim();
        var id = (businessId ?? string.Empty).Trim();
        if (type.Length == 0 || id.Length == 0)
        {
            return null;
        }

        var instance = await instanceRepository.GetQueryable()
            .Where(item => item.BusinessType == type && item.BusinessId == id)
            .OrderByDescending(item => item.CreationTime)
            .FirstAsync();
        if (instance is null)
        {
            return null;
        }

        var isRunning = instance.Status == ApprovalInstanceStatus.Running;
        return new ApprovalStatusSnapshot
        {
            InstanceId = instance.Id,
            Number = instance.Number,
            Status = instance.Status.ToString(),
            CurrentNodeName = isRunning ? instance.CurrentNodeName : null,
            InitiatorUserId = instance.InitiatorUserId,
            CreationTime = instance.CreationTime,
            CompletedTime = instance.CompletedTime,
            IsRunning = isRunning
        };
    }
}
