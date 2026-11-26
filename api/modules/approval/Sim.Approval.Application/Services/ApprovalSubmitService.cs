using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Security;
using Sim.Framework.Core.UnitOfWork;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 业务模块提交端口实现 —— 以当前登录用户身份调用引擎，业务模块无需感知审批内部结构。
/// </summary>
public sealed class ApprovalSubmitService(
    ApprovalEngine engine,
    ICurrentUser currentUser,
    ISqlSugarUnitOfWork unitOfWork) : IApprovalSubmitService
{
    public async Task<ApprovalSubmitResult> SubmitAsync(
        ApprovalSubmitInput input,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var instance = await engine.StartAsync(input, RequireUserId(), cancellationToken);
            return await BuildResultAsync(instance, cancellationToken);
        }, cancellationToken);
    }

    public async Task<ApprovalSubmitResult> ResubmitAsync(
        Guid instanceId,
        ApprovalSubmitInput input,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var instance = await engine.ResubmitAsync(
                instanceId,
                input.Title,
                input.Context,
                RequireUserId(),
                input.Fields,
                cancellationToken);
            return await BuildResultAsync(instance, cancellationToken);
        }, cancellationToken);
    }

    public Task WithdrawAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => engine.WithdrawAsync(instanceId, RequireUserId(), null, cancellationToken),
            cancellationToken);

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var result = await action();
            await unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        try
        {
            await action();
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Guid RequireUserId() =>
        currentUser.Id ?? throw new UnauthorizedAccessException("当前用户未登录。");

    private async Task<ApprovalSubmitResult> BuildResultAsync(
        ApprovalInstance instance,
        CancellationToken cancellationToken)
    {
        var assigneeIds = instance.Status == ApprovalInstanceStatus.Running
            ? await engine.GetCurrentPendingAssigneeIdsAsync(instance.Id, cancellationToken)
            : [];

        return new ApprovalSubmitResult
        {
            InstanceId = instance.Id,
            Number = instance.Number,
            Status = instance.Status.ToString(),
            CurrentAssigneeUserIds = assigneeIds
        };
    }
}
