using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Application.Services;
using Sim.System.Application.Contracts.Organization;
using Sim.System.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Organization;

namespace Sim.System.Application.Organization;

[Authorize]
[Permission(OrganizationPermissions.Manage)]
public class OrganizationAppService(
    ISqlSugarRepository<OrganizationUnitEntity> orgRepository,
    ISqlSugarRepository<OrganizationMembershipEntity> memberRepository,
    ISqlSugarRepository<RoleDataScopeEntity> scopeRepository) : ApplicationService, IOrganizationAppService
{
    /// <summary>
    /// 创建组织单元。
    /// </summary>
    public async Task<OrganizationUnitDto> CreateAsync(
        CreateOrganizationUnitDto input,
        CancellationToken cancellationToken = default)
    {
        // Validate code uniqueness within the same parent
        var existing = await orgRepository.GetAsync(o => o.ParentId == input.ParentId && o.Code == input.Code);
        if (existing != null)
            throw new BusinessException("Organization code already exists under the same parent.");

        // Build path and depth
        string path;
        int depth;
        if (input.ParentId.HasValue)
        {
            var parent = await orgRepository.GetAsync((object)input.ParentId.Value)
                ?? throw new BusinessException("Parent organization not found.");
            path = $"{parent.Path}/{input.Code}";
            depth = parent.Depth + 1;
        }
        else
        {
            path = $"/{input.Code}";
            depth = 0;
        }

        var entity = new OrganizationUnitEntity
        {
            Id = Guid.NewGuid(),
            ParentId = input.ParentId,
            Code = input.Code,
            Name = input.Name,
            Path = path,
            Depth = depth,
            ManagerUserId = input.ManagerUserId,
            SortOrder = input.SortOrder,
            IsActive = true,
            CreationTime = DateTime.UtcNow
        };

        await orgRepository.InsertAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// 更新指定的组织单元。
    /// </summary>
    public async Task<OrganizationUnitDto> UpdateAsync(
        Guid id,
        UpdateOrganizationUnitDto input,
        CancellationToken cancellationToken = default)
    {
        var entity = await orgRepository.GetAsync((object)id)
            ?? throw new BusinessException("Organization not found.");

        entity.Name = input.Name;
        entity.ManagerUserId = input.ManagerUserId;
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
        entity.LastModificationTime = DateTime.UtcNow;
        await orgRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// 删除指定的组织单元；存在下级组织时禁止删除。
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Check for children
        var children = await orgRepository.GetListAsync(o => o.ParentId == id);
        if (children.Count > 0)
            throw new BusinessException("Cannot delete organization with children. Remove children first.");

        await orgRepository.DeleteAsync(id);
        return true;
    }

    /// <summary>
    /// 获取指定的组织单元。
    /// </summary>
    public async Task<OrganizationUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await orgRepository.GetAsync((object)id);
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// 获取组织树，可按根组织筛选。
    /// </summary>
    public async Task<List<OrganizationUnitDto>> GetTreeAsync(
        Guid? rootId = null,
        CancellationToken cancellationToken = default)
    {
        List<OrganizationUnitEntity> entities;
        if (rootId.HasValue)
        {
            var root = await orgRepository.GetAsync((object)rootId.Value);
            if (root == null) return [];
            entities = await orgRepository.GetListAsync(o => o.Path.StartsWith(root.Path));
        }
        else
        {
            entities = await orgRepository.GetAll();
        }

        return entities.OrderBy(e => e.Path).ThenBy(e => e.SortOrder).Select(MapToDto).ToList();
    }

    /// <summary>
    /// 获取指定组织单元的全部下级组织。
    /// </summary>
    public async Task<List<OrganizationUnitDto>> GetDescendantsAsync(
        Guid rootId,
        CancellationToken cancellationToken = default)
    {
        var root = await orgRepository.GetAsync((object)rootId)
            ?? throw new BusinessException("Organization not found.");
        var descendants = await orgRepository.GetListAsync(
            o => o.Path.StartsWith(root.Path) && o.Id != rootId);
        return descendants.OrderBy(e => e.Path).Select(MapToDto).ToList();
    }

    /// <summary>
    /// 添加组织成员关系。
    /// </summary>
    public async Task<OrganizationMemberDto> AddMemberAsync(
        AddMemberDto input,
        CancellationToken cancellationToken = default)
    {
        // If primary, unset existing primary
        if (input.IsPrimary)
        {
            var existingPrimary = await memberRepository.GetListAsync(
                m => m.UserId == input.UserId && m.IsPrimary);
            foreach (var ep in existingPrimary)
            {
                ep.IsPrimary = false;
                ep.LastModificationTime = DateTime.UtcNow;
                await memberRepository.UpdateAsync(ep);
            }
        }

        var entity = new OrganizationMembershipEntity
        {
            Id = Guid.NewGuid(),
            OrganizationUnitId = input.OrganizationUnitId,
            UserId = input.UserId,
            IsPrimary = input.IsPrimary,
            PositionName = input.PositionName,
            MembershipType = input.IsPrimary ? "Primary" : "Concurrent",
            CreationTime = DateTime.UtcNow
        };

        await memberRepository.InsertAsync(entity);
        return new OrganizationMemberDto
        {
            Id = entity.Id,
            OrganizationUnitId = entity.OrganizationUnitId,
            UserId = entity.UserId,
            IsPrimary = entity.IsPrimary,
            PositionName = entity.PositionName,
            MembershipType = entity.MembershipType
        };
    }

    /// <summary>
    /// 移除指定的组织成员关系。
    /// </summary>
    public async Task<bool> RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        await memberRepository.DeleteAsync(membershipId);
        return true;
    }

    /// <summary>
    /// 获取指定组织单元的成员列表。
    /// </summary>
    public async Task<List<OrganizationMemberDto>> GetMembersAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        var members = await memberRepository.GetListAsync(m => m.OrganizationUnitId == organizationUnitId);
        return members.Select(m => new OrganizationMemberDto
        {
            Id = m.Id,
            OrganizationUnitId = m.OrganizationUnitId,
            UserId = m.UserId,
            IsPrimary = m.IsPrimary,
            PositionName = m.PositionName,
            MembershipType = m.MembershipType
        }).ToList();
    }

    /// <summary>
    /// 获取指定用户所属的组织列表。
    /// </summary>
    public async Task<List<OrganizationMemberDto>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await memberRepository.GetListAsync(m => m.UserId == userId);
        return memberships.Select(m => new OrganizationMemberDto
        {
            Id = m.Id,
            OrganizationUnitId = m.OrganizationUnitId,
            UserId = m.UserId,
            IsPrimary = m.IsPrimary,
            PositionName = m.PositionName,
            MembershipType = m.MembershipType
        }).ToList();
    }

    /// <summary>
    /// 设置角色在指定资源上的数据范围。
    /// </summary>
    public async Task<RoleDataScopeDto> SetRoleDataScopeAsync(
        SetRoleDataScopeDto input,
        CancellationToken cancellationToken = default)
    {
        var existing = await scopeRepository.GetAsync(
            s => s.RoleId == input.RoleId && s.Resource == input.Resource);

        RoleDataScopeEntity entity;
        if (existing != null)
        {
            existing.ScopeType = input.ScopeType;
            existing.IncludeDescendants = input.IncludeDescendants;
            existing.CustomOrganizationIds = input.CustomOrganizationIds != null
                ? string.Join(",", input.CustomOrganizationIds)
                : null;
            existing.LastModificationTime = DateTime.UtcNow;
            await scopeRepository.UpdateAsync(existing);
            entity = existing;
        }
        else
        {
            entity = new RoleDataScopeEntity
            {
                Id = Guid.NewGuid(),
                RoleId = input.RoleId,
                Resource = input.Resource,
                ScopeType = input.ScopeType,
                IncludeDescendants = input.IncludeDescendants,
                CustomOrganizationIds = input.CustomOrganizationIds != null
                    ? string.Join(",", input.CustomOrganizationIds)
                    : null,
                CreationTime = DateTime.UtcNow
            };
            await scopeRepository.InsertAsync(entity);
        }

        return new RoleDataScopeDto
        {
            Id = entity.Id,
            RoleId = entity.RoleId,
            Resource = entity.Resource,
            ScopeType = entity.ScopeType,
            IncludeDescendants = entity.IncludeDescendants,
            CustomOrganizationIds = entity.CustomOrganizationIds
        };
    }

    /// <summary>
    /// 获取角色的数据范围配置列表。
    /// </summary>
    public async Task<List<RoleDataScopeDto>> GetRoleDataScopesAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var scopes = await scopeRepository.GetListAsync(s => s.RoleId == roleId);
        return scopes.Select(s => new RoleDataScopeDto
        {
            Id = s.Id,
            RoleId = s.RoleId,
            Resource = s.Resource,
            ScopeType = s.ScopeType,
            IncludeDescendants = s.IncludeDescendants,
            CustomOrganizationIds = s.CustomOrganizationIds
        }).ToList();
    }

    /// <summary>
    /// 删除指定的角色数据范围配置。
    /// </summary>
    public async Task<bool> DeleteRoleDataScopeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await scopeRepository.DeleteAsync(id);
        return true;
    }

    private static OrganizationUnitDto MapToDto(OrganizationUnitEntity entity) => new()
    {
        Id = entity.Id,
        ParentId = entity.ParentId,
        Code = entity.Code,
        Name = entity.Name,
        Path = entity.Path,
        Depth = entity.Depth,
        ManagerUserId = entity.ManagerUserId,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        CreationTime = entity.CreationTime
    };
}
