using Microsoft.AspNetCore.Authorization;
using Sim.Approval.Application.Contracts;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批实例监控（管理端）—— 跨用户查询全部实例并强制终止。
/// 查看与终止分别由独立权限控制。
/// </summary>
[Authorize]
public class MonitorAppService(
    ApprovalEngine engine,
    ApprovalQueryService queryService) : ApplicationService, IMonitorAppService
{
    /// <summary>全部审批实例分页。</summary>
    [Permission(ApprovalPermissions.MonitorInstance)]
    public Task<PagedResultDto<ApprovalInstanceListDto>> GetListAsync(ApprovalInstanceQueryDto input) =>
        queryService.GetMonitorListAsync(input);

    /// <summary>任意实例明细（含任务与记录）。</summary>
    [Permission(ApprovalPermissions.MonitorInstance)]
    public Task<ApprovalInstanceDetailDto> GetAsync(Guid id) =>
        queryService.GetDetailAsync(id, RequireUserId(), canMonitor: true);

    /// <summary>强制终止进行中的实例。</summary>
    [Permission(ApprovalPermissions.TerminateInstance)]
    [SqlSugarUnitOfWork]
    public Task TerminateAsync(Guid id, ApprovalTerminateDto input) =>
        engine.TerminateAsync(id, RequireUserId(), input.Comment);

    private Guid RequireUserId() =>
        CurrentUser.Id ?? throw new UnauthorizedAccessException("当前用户未登录。");
}
