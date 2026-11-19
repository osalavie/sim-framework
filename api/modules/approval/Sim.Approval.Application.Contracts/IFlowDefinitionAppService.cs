using Sim.Approval.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Approval.Application.Contracts;

/// <summary>
/// 审批流程定义管理 — 创建、编辑、发布与停用流程定义。
/// 流程定义按版本管理：发布后不可修改，调整需基于当前版本创建新草稿版本。
/// </summary>
public interface IFlowDefinitionAppService : IApplicationService
{
    /// <summary>
    /// 获取全部流程定义（含草稿、已发布与已停用，按编码与版本倒序）。
    /// </summary>
    /// <returns>流程定义列表。</returns>
    Task<List<ApprovalFlowDefinitionListDto>> GetListAsync();

    /// <summary>
    /// 获取流程定义明细（含节点）。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    /// <returns>流程定义明细。</returns>
    Task<ApprovalFlowDefinitionDto> GetAsync(Guid id);

    /// <summary>
    /// 创建流程定义（草稿，版本号从 1 开始；同编码的最新版本号 +1）。
    /// </summary>
    /// <param name="input">创建数据与节点列表。</param>
    /// <returns>创建后的流程定义明细。</returns>
    Task<ApprovalFlowDefinitionDto> CreateAsync(CreateApprovalFlowDefinitionDto input);

    /// <summary>
    /// 更新草稿流程定义（节点整体覆盖）。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    /// <param name="input">更新数据与节点列表。</param>
    /// <returns>更新后的流程定义明细。</returns>
    Task<ApprovalFlowDefinitionDto> UpdateAsync(Guid id, UpdateApprovalFlowDefinitionDto input);

    /// <summary>
    /// 发布前校验（不改变状态，不抛异常）：按图结构规则返回逐条问题，供设计器展示。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    /// <returns>校验结果（问题清单为空表示可发布）。</returns>
    Task<ApprovalFlowValidationResultDto> ValidateAsync(Guid id);

    /// <summary>
    /// 发布流程定义，发布后不可修改。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    /// <returns>发布后的流程定义明细。</returns>
    Task<ApprovalFlowDefinitionDto> PublishAsync(Guid id);

    /// <summary>
    /// 停用已发布流程定义（存在进行中实例时不允许停用）。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    /// <returns>停用后的流程定义明细。</returns>
    Task<ApprovalFlowDefinitionDto> DisableAsync(Guid id);

    /// <summary>
    /// 基于已发布版本创建新草稿版本（版本号 +1）。
    /// </summary>
    /// <param name="id">来源流程定义 ID。</param>
    /// <returns>新草稿版本明细。</returns>
    Task<ApprovalFlowDefinitionDto> CreateVersionAsync(Guid id);

    /// <summary>
    /// 删除草稿流程定义（已发布或已停用版本不可删除）。
    /// </summary>
    /// <param name="id">流程定义 ID。</param>
    Task DeleteAsync(Guid id);
}
