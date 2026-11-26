using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批附件服务 —— 文件本体交给框架既有存储链路（类型校验、隔离扫描、元数据登记），
/// 本模块只记录「某实例/某任务引用了哪个存储对象」。
/// <para>
/// 下载不经内容中心的资源访问策略，也不使用存储签名的限时地址：审批侧按实例可见范围
/// （发起人、任一任务处理人、抄送人）自行鉴权后代理读取文件，处理人无需业务单据权限。
/// </para>
/// </summary>
public sealed class ApprovalAttachmentService(
    IApprovalAttachmentRepository attachmentRepository,
    IApprovalInstanceRepository instanceRepository,
    IApprovalTaskRepository taskRepository,
    IApprovalCcRepository ccRepository,
    IFileManagementService fileManagementService)
{
    /// <summary>上传实例级附件（发起人，且实例处于处理中或已退回）。</summary>
    public async Task<ApprovalAttachmentDto> UploadForInstanceAsync(
        Guid instanceId,
        Guid uploaderUserId,
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default)
    {
        var instance = await LoadRequiredInstanceAsync(instanceId, cancellationToken);
        if (instance.InitiatorUserId != uploaderUserId)
        {
            throw new BusinessException(
                ApprovalErrorCodes.NotInitiator,
                $"审批实例 {instance.Number} 的实例级附件只能由发起人上传。");
        }

        if (instance.Status is not (ApprovalInstanceStatus.Running or ApprovalInstanceStatus.Returned))
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotRunning,
                $"审批实例 {instance.Number} 已结束，不能再上传附件。");
        }

        return await SaveAsync(instance.Id, null, uploaderUserId, file, cancellationToken);
    }

    /// <summary>上传任务附件（该任务的处理人，且实例仍在处理中）。</summary>
    public async Task<ApprovalAttachmentDto> UploadForTaskAsync(
        Guid taskId,
        Guid uploaderUserId,
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default)
    {
        var task = await taskRepository.GetAsync(taskId)
            ?? throw new BusinessException(ApprovalErrorCodes.TaskNotFound, $"审批任务 {taskId} 不存在。");
        if (task.AssigneeUserId != uploaderUserId)
        {
            throw new BusinessException(ApprovalErrorCodes.NotTaskAssignee, "只有该任务的处理人可以上传审批附件。");
        }

        var instance = await LoadRequiredInstanceAsync(task.InstanceId, cancellationToken);
        if (instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotRunning,
                $"审批实例 {instance.Number} 已结束，不能再上传附件。");
        }

        return await SaveAsync(instance.Id, task.Id, uploaderUserId, file, cancellationToken);
    }

    /// <summary>实例附件列表（按上传时间升序）。</summary>
    public async Task<List<ApprovalAttachmentDto>> ListAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var rows = await attachmentRepository.GetListAsync(item => item.InstanceId == instanceId);
        return rows.OrderBy(item => item.CreationTime).Select(Map).ToList();
    }

    /// <summary>
    /// 下载附件：仅实例可见范围内（发起人、任一任务处理人、抄送人）可以读取，
    /// 不可见时与不存在同样返回 <see cref="ApprovalErrorCodes.AttachmentNotFound"/>，避免探测。
    /// </summary>
    public async Task<IRemoteStreamContent> DownloadAsync(
        Guid attachmentId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await attachmentRepository.GetAsync(attachmentId)
            ?? throw NotFound();
        var instance = await LoadRequiredInstanceAsync(attachment.InstanceId, cancellationToken);
        if (!await CanViewAsync(instance, viewerUserId))
        {
            throw NotFound();
        }

        return await fileManagementService.GetDownloadAsync(
            attachment.FileId,
            attachment.Provider,
            cancellationToken);
    }

    private async Task<ApprovalAttachmentDto> SaveAsync(
        Guid instanceId,
        Guid? taskId,
        Guid uploaderUserId,
        IRemoteStreamContent file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            throw new BusinessException(ApprovalErrorCodes.InvalidRequest, "上传的文件不能为空。");
        }

        var stored = await fileManagementService.UploadAsync(file, cancellationToken: cancellationToken);
        var entity = new ApprovalAttachment
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            TaskId = taskId,
            FileId = stored.Key,
            Provider = stored.Provider,
            FileName = Truncate(stored.FileName, ApprovalConsts.MaxAttachmentFileNameLength),
            Size = stored.Length,
            UploaderUserId = uploaderUserId
        };
        try
        {
            await attachmentRepository.InsertAsync(entity);
        }
        catch
        {
            try
            {
                await fileManagementService.DeleteAsync(stored.Key, stored.Provider, CancellationToken.None);
            }
            catch
            {
                // 保留原始数据库异常；存储侧孤儿文件由文件生命周期清理任务兜底。
            }

            throw;
        }
        return Map(entity);
    }

    private async Task<bool> CanViewAsync(ApprovalInstance instance, Guid viewerUserId)
    {
        if (instance.InitiatorUserId == viewerUserId)
        {
            return true;
        }

        var tasks = await taskRepository.GetListAsync(item => item.InstanceId == instance.Id);
        if (tasks.Any(item => item.AssigneeUserId == viewerUserId))
        {
            return true;
        }

        var ccs = await ccRepository.GetListAsync(item => item.InstanceId == instance.Id);
        return ccs.Any(item => item.UserId == viewerUserId);
    }

    private async Task<ApprovalInstance> LoadRequiredInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var instance = await instanceRepository.GetAsync(instanceId);
        return instance
            ?? throw new BusinessException(ApprovalErrorCodes.InstanceNotFound, $"审批实例 {instanceId} 不存在。");
    }

    private static ApprovalAttachmentDto Map(ApprovalAttachment row) => new()
    {
        Id = row.Id,
        InstanceId = row.InstanceId,
        TaskId = row.TaskId,
        FileName = row.FileName,
        Size = row.Size,
        UploaderUserId = row.UploaderUserId,
        CreationTime = row.CreationTime
    };

    private static BusinessException NotFound() =>
        new(ApprovalErrorCodes.AttachmentNotFound, "审批附件不存在或无权访问。");

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
