using Sim.Approval.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Approval.Application.Contracts;

/// <summary>
/// 业务类型目录 — 展示已注册审批的业务类型及其可条件字段，供流程定义与发起页面使用。
/// </summary>
public interface IBusinessTypeAppService : IApplicationService
{
    /// <summary>
    /// 获取全部已注册的审批业务类型。
    /// </summary>
    /// <returns>业务类型列表（含可条件字段）。</returns>
    Task<List<ApprovalBusinessTypeDto>> GetListAsync();

    /// <summary>搜索当前租户内启用账号，供审批人、转交和加签选择使用。</summary>
    Task<List<ApprovalUserOptionDto>> GetUsersAsync(string? keyword = null, int maxResultCount = 20);

    /// <summary>当前租户内启用组织的扁平列表，供审批节点的组织负责人/组织人员来源选择组织。</summary>
    Task<List<ApprovalOrganizationOptionDto>> GetOrganizationsAsync();
}
