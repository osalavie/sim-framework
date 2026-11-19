using Sim.Approval.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Approval.Application.Contracts;

/// <summary>
/// 审批中心（用户端）— 发起、待办、已办、我发起的、抄送我的与审批操作。
/// </summary>
public interface IInstanceAppService : IApplicationService
{
    /// <summary>
    /// 发起审批实例（同一业务对象同时只允许一个进行中的实例）。
    /// </summary>
    /// <param name="input">发起数据（流程编码、业务类型与标识、标题、业务上下文）。</param>
    /// <returns>创建后的实例摘要。</returns>
    Task<ApprovalInstanceListDto> SubmitAsync(SubmitApprovalDto input);

    /// <summary>
    /// 重新提交被退回的实例（复用原实例，流程重新从首个节点开始）。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <param name="input">重新提交的数据（可更新标题与业务上下文）。</param>
    /// <returns>更新后的实例摘要。</returns>
    Task<ApprovalInstanceListDto> ResubmitAsync(Guid id, SubmitApprovalDto input);

    /// <summary>
    /// 分页获取我的待办。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>待办分页结果。</returns>
    Task<PagedResultDto<ApprovalTaskDto>> GetMyPendingAsync(ApprovalTaskQueryDto input);

    /// <summary>
    /// 分页获取我的已办（已通过、已拒绝与已取消任务）。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>已办分页结果。</returns>
    Task<PagedResultDto<ApprovalTaskDto>> GetMyHandledAsync(ApprovalTaskQueryDto input);

    /// <summary>
    /// 分页获取我发起的实例。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>实例分页结果。</returns>
    Task<PagedResultDto<ApprovalInstanceListDto>> GetMySubmittedAsync(ApprovalInstanceQueryDto input);

    /// <summary>
    /// 分页获取抄送我的实例。
    /// </summary>
    /// <param name="input">查询条件。</param>
    /// <returns>抄送分页结果。</returns>
    Task<PagedResultDto<ApprovalCcDto>> GetMyCcAsync(ApprovalInstanceQueryDto input);

    /// <summary>
    /// 获取待办、未读抄送与进行中发起的数量（角标）。
    /// </summary>
    /// <returns>数量统计。</returns>
    Task<ApprovalInstanceCountsDto> GetCountsAsync();

    /// <summary>
    /// 获取实例明细（发起人、处理人、抄送人或具备监控权限的用户可见）。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <returns>实例明细。</returns>
    Task<ApprovalInstanceDetailDto> GetAsync(Guid id);

    /// <summary>
    /// 上传实例级附件（仅发起人，且实例处于处理中或已退回）。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <param name="file">上传的文件。</param>
    /// <returns>附件记录。</returns>
    Task<ApprovalAttachmentDto> UploadAttachmentAsync(Guid id, IRemoteStreamContent file);

    /// <summary>
    /// 上传当前任务的附件（仅该任务的待处理处理人）。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="file">上传的文件。</param>
    /// <returns>附件记录。</returns>
    Task<ApprovalAttachmentDto> UploadTaskAttachmentAsync(Guid taskId, IRemoteStreamContent file);

    /// <summary>
    /// 下载实例附件（发起人、任一任务处理人与抄送人可下载）。
    /// </summary>
    /// <param name="attachmentId">附件记录 ID。</param>
    /// <returns>文件内容（以附件名下载）。</returns>
    Task<IRemoteStreamContent> GetAttachmentDownloadAsync(Guid attachmentId);

    /// <summary>
    /// 同意当前任务。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">处理意见。</param>
    Task ApproveAsync(Guid taskId, ApprovalActionDto input);

    /// <summary>
    /// 拒绝当前任务（实例立即被拒绝并结束）。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">处理意见。</param>
    Task RejectAsync(Guid taskId, ApprovalActionDto input);

    /// <summary>
    /// 将当前任务转交他人处理。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">目标用户与处理意见。</param>
    Task TransferAsync(Guid taskId, ApprovalTransferDto input);

    /// <summary>
    /// 为当前任务加签（前加签先于本人审批，后加签在本人通过后审批）。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">目标用户、加签位置与处理意见。</param>
    Task AddSignAsync(Guid taskId, ApprovalAddSignDto input);

    /// <summary>
    /// 退回上一步（回到最近一个已通过的节点重新审批）。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">处理意见。</param>
    Task ReturnToPreviousAsync(Guid taskId, ApprovalActionDto input);

    /// <summary>
    /// 退回发起人（实例进入被退回状态，等待发起人重新提交）。
    /// </summary>
    /// <param name="taskId">任务 ID。</param>
    /// <param name="input">处理意见。</param>
    Task ReturnToInitiatorAsync(Guid taskId, ApprovalActionDto input);

    /// <summary>
    /// 撤回我发起的实例（仅流程允许且实例处于进行中）。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <param name="input">撤回说明。</param>
    Task WithdrawAsync(Guid id, ApprovalActionDto input);

    /// <summary>
    /// 催办实例的当前处理人。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    Task UrgeAsync(Guid id);

    /// <summary>
    /// 标记抄送记录已读。
    /// </summary>
    /// <param name="ccId">抄送记录 ID。</param>
    Task MarkCcReadAsync(Guid ccId);
}
