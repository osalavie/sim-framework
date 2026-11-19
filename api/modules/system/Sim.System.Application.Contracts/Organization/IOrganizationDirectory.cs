namespace Sim.System.Application.Contracts.Organization;

/// <summary>
/// 组织目录稳定端口 —— 供其他模块解析用户的主组织、向上级组织负责人链与角色成员，
/// 避免跨模块直接访问组织相关数据表。
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>
    /// 获取用户的主组织单位 ID。
    /// </summary>
    /// <param name="userId">用户 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>主组织单位 ID；用户未加入任何组织时返回 null。</returns>
    Task<Guid?> FindPrimaryOrganizationUnitIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户向上最近的负责人链（按由近及远排序，已排除用户本人）。
    /// </summary>
    /// <param name="userId">用户 ID。</param>
    /// <param name="organizationUnitId">起始组织单位 ID；为空时使用用户主组织。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>负责人用户 ID 列表（可能为空）。</returns>
    Task<IReadOnlyList<Guid>> FindManagerChainAsync(
        Guid userId,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定组织的负责人用户 ID。
    /// </summary>
    /// <param name="organizationUnitId">组织单位 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>负责人用户 ID；组织不存在、未设负责人或负责人已停用时返回 null。</returns>
    Task<Guid?> FindOrganizationManagerAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定组织的直接成员用户 ID（仅启用用户，不含下级组织成员）。
    /// </summary>
    /// <param name="organizationUnitId">组织单位 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户 ID 列表（可能为空）。</returns>
    Task<IReadOnlyList<Guid>> FindDirectMemberUserIdsAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前租户内全部启用组织的扁平列表，供审批流程设计器选择组织。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<OrganizationDirectoryUnit>> ListOrganizationUnitsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定角色下的全部启用用户 ID（按角色代码或角色名称匹配）。
    /// </summary>
    /// <param name="roleName">角色代码或角色名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户 ID 列表（可能为空）。</returns>
    Task<IReadOnlyList<Guid>> FindUserIdsByRoleNameAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>判断用户是否存在、启用且属于当前租户范围。</summary>
    Task<bool> IsActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>按关键字搜索当前租户内启用用户，供审批选人等跨模块场景使用。</summary>
    Task<IReadOnlyList<OrganizationDirectoryUser>> SearchActiveUsersAsync(
        string? keyword,
        int maxResultCount = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>跨模块使用的最小用户目录投影，不暴露联系方式与账号管理字段。</summary>
public sealed record OrganizationDirectoryUser(Guid Id, string UserName, string DisplayName);

/// <summary>跨模块使用的最小组织目录投影，供审批等场景选择组织。</summary>
public sealed record OrganizationDirectoryUnit(Guid Id, string Name, Guid? ParentId);
