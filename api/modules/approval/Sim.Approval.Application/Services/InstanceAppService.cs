using Microsoft.AspNetCore.Authorization;
using Sim.Approval.Application.Contracts;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批中心（用户端）—— 发起、待办、已办、我发起的、抄送我的与审批操作。
/// 所有写操作在同一个工作单元事务内完成，失败整体回滚。
/// </summary>
[Authorize]
public class InstanceAppService(
    ApprovalEngine engine,
    ApprovalQueryService queryService,
    ApprovalAttachmentService attachmentService) : ApplicationService, IInstanceAppService
{
    /// <summary>发起审批实例。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalInstanceListDto> SubmitAsync(SubmitApprovalDto input)
    {
        var instance = await engine.StartAsync(ToSubmitInput(input), RequireUserId());
        return ApprovalQueryService.MapToInstanceList(instance);
    }

    /// <summary>重新提交被退回的实例。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalInstanceListDto> ResubmitAsync(Guid id, SubmitApprovalDto input)
    {
        var instance = await engine.ResubmitAsync(
            id,
            input.Title,
            ToContext(input.Context),
            RequireUserId(),
            input.Fields);
        return ApprovalQueryService.MapToInstanceList(instance);
    }

    /// <summary>我的待办。</summary>
    public Task<PagedResultDto<ApprovalTaskDto>> GetMyPendingAsync(ApprovalTaskQueryDto input) =>
        queryService.GetMyPendingAsync(RequireUserId(), input);

    /// <summary>我的已办。</summary>
    public Task<PagedResultDto<ApprovalTaskDto>> GetMyHandledAsync(ApprovalTaskQueryDto input) =>
        queryService.GetMyHandledAsync(RequireUserId(), input);

    /// <summary>我发起的实例。</summary>
    public Task<PagedResultDto<ApprovalInstanceListDto>> GetMySubmittedAsync(ApprovalInstanceQueryDto input) =>
        queryService.GetMySubmittedAsync(RequireUserId(), input);

    /// <summary>抄送我的实例。</summary>
    public Task<PagedResultDto<ApprovalCcDto>> GetMyCcAsync(ApprovalInstanceQueryDto input) =>
        queryService.GetMyCcAsync(RequireUserId(), input);

    /// <summary>角标数量。</summary>
    public Task<ApprovalInstanceCountsDto> GetCountsAsync() => queryService.GetCountsAsync(RequireUserId());

    /// <summary>实例明细（发起人、处理人与抄送人可见；监控查看走管理端接口）。</summary>
    public Task<ApprovalInstanceDetailDto> GetAsync(Guid id) =>
        queryService.GetDetailAsync(id, RequireUserId(), canMonitor: false);

    /// <summary>上传实例级附件（发起人，实例处理中或已退回）。</summary>
    [SqlSugarUnitOfWork]
    public Task<ApprovalAttachmentDto> UploadAttachmentAsync(Guid id, IRemoteStreamContent file) =>
        attachmentService.UploadForInstanceAsync(id, RequireUserId(), file);

    /// <summary>上传任务附件（该任务的处理人）。</summary>
    [SqlSugarUnitOfWork]
    public Task<ApprovalAttachmentDto> UploadTaskAttachmentAsync(Guid taskId, IRemoteStreamContent file) =>
        attachmentService.UploadForTaskAsync(taskId, RequireUserId(), file);

    /// <summary>下载实例附件（实例可见范围内）。</summary>
    public Task<IRemoteStreamContent> GetAttachmentDownloadAsync(Guid attachmentId) =>
        attachmentService.DownloadAsync(attachmentId, RequireUserId());

    /// <summary>同意当前任务。</summary>
    [SqlSugarUnitOfWork]
    public Task ApproveAsync(Guid taskId, ApprovalActionDto input) =>
        engine.ApproveAsync(taskId, RequireUserId(), NormalizeComment(input?.Comment));

    /// <summary>拒绝当前任务。</summary>
    [SqlSugarUnitOfWork]
    public Task RejectAsync(Guid taskId, ApprovalActionDto input) =>
        engine.RejectAsync(taskId, RequireUserId(), NormalizeComment(input?.Comment));

    /// <summary>转交当前任务。</summary>
    [SqlSugarUnitOfWork]
    public Task TransferAsync(Guid taskId, ApprovalTransferDto input) =>
        engine.TransferAsync(taskId, RequireUserId(), input.TargetUserId, NormalizeComment(input.Comment));

    /// <summary>为当前任务加签。</summary>
    [SqlSugarUnitOfWork]
    public Task AddSignAsync(Guid taskId, ApprovalAddSignDto input) =>
        engine.AddSignAsync(
            taskId,
            RequireUserId(),
            input.TargetUserId,
            ParseAddSignPosition(input.Position),
            NormalizeComment(input.Comment));

    /// <summary>退回上一步。</summary>
    [SqlSugarUnitOfWork]
    public Task ReturnToPreviousAsync(Guid taskId, ApprovalActionDto input) =>
        engine.ReturnToPreviousAsync(taskId, RequireUserId(), NormalizeComment(input?.Comment));

    /// <summary>退回发起人。</summary>
    [SqlSugarUnitOfWork]
    public Task ReturnToInitiatorAsync(Guid taskId, ApprovalActionDto input) =>
        engine.ReturnToInitiatorAsync(taskId, RequireUserId(), NormalizeComment(input?.Comment));

    /// <summary>撤回实例。</summary>
    [SqlSugarUnitOfWork]
    public Task WithdrawAsync(Guid id, ApprovalActionDto input) =>
        engine.WithdrawAsync(id, RequireUserId(), NormalizeComment(input?.Comment));

    /// <summary>终止实例（仅发起人的上级；管理权限走监控端接口）。</summary>
    [SqlSugarUnitOfWork]
    public Task TerminateAsync(Guid id, ApprovalTerminateDto input) =>
        engine.TerminateAsync(id, RequireUserId(), NormalizeComment(input?.Comment) ?? string.Empty);

    /// <summary>催办当前处理人。</summary>
    [SqlSugarUnitOfWork]
    public Task UrgeAsync(Guid id) => engine.UrgeAsync(id, RequireUserId());

    /// <summary>标记抄送记录已读。</summary>
    [SqlSugarUnitOfWork]
    public Task MarkCcReadAsync(Guid ccId) => engine.MarkCcReadAsync(ccId, RequireUserId());

    private Guid RequireUserId() =>
        CurrentUser.Id ?? throw new UnauthorizedAccessException("当前用户未登录。");

    private static ApprovalSubmitInput ToSubmitInput(SubmitApprovalDto input) => new()
    {
        DefinitionCode = input.DefinitionCode,
        BusinessType = input.BusinessType,
        BusinessId = input.BusinessId,
        Title = input.Title,
        Context = ToContext(input.Context),
        Fields = input.Fields
    };

    private static Dictionary<string, string> ToContext(Dictionary<string, string>? context) =>
        context is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(context, StringComparer.Ordinal);

    private static string? NormalizeComment(string? comment)
    {
        var value = comment?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length > ApprovalConsts.MaxCommentLength ? value[..ApprovalConsts.MaxCommentLength] : value;
    }

    private static ApprovalAddSignPosition ParseAddSignPosition(string? position)
    {
        if (Enum.TryParse<ApprovalAddSignPosition>(position?.Trim(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new BusinessException(
            ApprovalErrorCodes.InvalidRequest,
            $"加签位置 [{position}] 不合法，仅支持 Before 与 After。");
    }
}
