using Microsoft.AspNetCore.Authorization;
using Sim.Approval.Application.Contracts;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.System.Application.Contracts.Organization;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 业务类型目录 —— 展示已注册审批的业务类型及其可条件字段。
/// 数据来源于各业务模块注册的 <see cref="Contracts.Ports.IApprovalBusinessDescriptor"/>。
/// </summary>
[Authorize]
public class BusinessTypeAppService(
    ApprovalBusinessRegistry businessRegistry,
    IOrganizationDirectory organizationDirectory) :
    ApplicationService,
    IBusinessTypeAppService
{
    /// <summary>全部已注册的审批业务类型。</summary>
    [RateLimit(permitLimit: 30, windowSeconds: 60, PartitionName = "approval-business-types")]
    public Task<List<ApprovalBusinessTypeDto>> GetListAsync()
    {
        var result = businessRegistry.Descriptors
            .Select(item => new ApprovalBusinessTypeDto
            {
                BusinessType = item.BusinessType,
                DisplayName = item.DisplayName,
                DefinitionCode = item.DefinitionCode,
                Fields = item.Fields.ToList()
            })
            .ToList();

        return Task.FromResult(result);
    }

    [RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "approval-user-directory")]
    public async Task<List<ApprovalUserOptionDto>> GetUsersAsync(
        string? keyword = null,
        int maxResultCount = 20)
    {
        var users = await organizationDirectory.SearchActiveUsersAsync(keyword, maxResultCount);
        return users.Select(user => new ApprovalUserOptionDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName
        }).ToList();
    }

    [RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "approval-organization-directory")]
    public async Task<List<ApprovalOrganizationOptionDto>> GetOrganizationsAsync()
    {
        var units = await organizationDirectory.ListOrganizationUnitsAsync();
        return units.Select(unit => new ApprovalOrganizationOptionDto
        {
            Id = unit.Id,
            Name = unit.Name,
            ParentId = unit.ParentId
        }).ToList();
    }
}
