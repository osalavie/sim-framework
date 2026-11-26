namespace Sim.System.Application.Contracts.Organization;

public sealed class OrganizationUnitDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; }
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
}

public sealed class CreateOrganizationUnitDto
{
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateOrganizationUnitDto
{
    public string Name { get; set; } = string.Empty;
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OrganizationMemberDto
{
    public Guid Id { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public string? PositionName { get; set; }
    public string MembershipType { get; set; } = string.Empty;
}

public sealed class AddMemberDto
{
    public Guid OrganizationUnitId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public string? PositionName { get; set; }
}

public sealed class RoleDataScopeDto
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public bool IncludeDescendants { get; set; }
    public string? CustomOrganizationIds { get; set; }
}

public sealed class SetRoleDataScopeDto
{
    public Guid RoleId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Self";
    public bool IncludeDescendants { get; set; }
    public List<Guid>? CustomOrganizationIds { get; set; }
}
