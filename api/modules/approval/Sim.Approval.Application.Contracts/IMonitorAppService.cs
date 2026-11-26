using Sim.Approval.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Approval.Application.Contracts;

/// <summary>
/// 审批实例监控（管理端）— 跨用户查询全部实例并强制终止。
/// </summary>
public interface IMonitorAppService : IApplicationService
{
    /// <summary>
    /// 分页查询全部审批实例。
    /// </summary>
    /// <param name="input">查询条件（状态、业务类型、流程编码、发起人、关键字）。</param>
    /// <returns>实例分页结果。</returns>
    Task<PagedResultDto<ApprovalInstanceListDto>> GetListAsync(ApprovalInstanceQueryDto input);

    /// <summary>
    /// 获取任意实例明细（含任务与记录）。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <returns>实例明细。</returns>
    Task<ApprovalInstanceDetailDto> GetAsync(Guid id);

    /// <summary>
    /// 强制终止进行中的实例。
    /// </summary>
    /// <param name="id">审批实例 ID。</param>
    /// <param name="input">终止原因。</param>
    Task TerminateAsync(Guid id, ApprovalTerminateDto input);
}
