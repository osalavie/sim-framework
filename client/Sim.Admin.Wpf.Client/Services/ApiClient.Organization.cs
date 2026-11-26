using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<List<OrganizationUnit>> GetOrganizationTreeAsync() =>
        SendJsonAsync<List<OrganizationUnit>>(HttpMethod.Get, "api/system/organization/tree");

    public Task<OrganizationUnit> CreateOrganizationAsync(OrganizationCreate input) =>
        SendJsonAsync<OrganizationUnit>(HttpMethod.Post, "api/system/organization", input);

    public Task<OrganizationUnit> UpdateOrganizationAsync(Guid id, OrganizationUpdate input) =>
        SendJsonAsync<OrganizationUnit>(HttpMethod.Put, $"api/system/organization/{id:D}", input);

    public async Task DeleteOrganizationAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/system/organization/{id:D}");
    }

    public Task<List<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationUnitId) =>
        SendJsonAsync<List<OrganizationMember>>(HttpMethod.Get,
            $"api/system/organization/members/{organizationUnitId:D}");

    public Task<OrganizationMember> AddOrganizationMemberAsync(OrganizationMemberCreate input) =>
        SendJsonAsync<OrganizationMember>(HttpMethod.Post, "api/system/organization/member", input);

    public async Task RemoveOrganizationMemberAsync(Guid membershipId)
    {
        using var response = await SendAsync(HttpMethod.Delete,
            $"api/system/organization/member/{membershipId:D}");
    }

    public Task<PagedResult<ApiUserDetail>> GetSystemUsersAsync(string keyword) =>
        SendJsonAsync<PagedResult<ApiUserDetail>>(HttpMethod.Get,
            Query("api/system/user", ("keyword", keyword), ("maxResultCount", "20")));
}

public sealed class OrganizationUnit
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public int Depth { get; set; }
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class OrganizationCreate
{
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class OrganizationUpdate
{
    public string? Name { get; set; }
    public Guid? ManagerUserId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class OrganizationMember
{
    public Guid Id { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public string? PositionName { get; set; }
    public string? MembershipType { get; set; }
}

public sealed class OrganizationMemberCreate
{
    public Guid OrganizationUnitId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public string? PositionName { get; set; }
}
