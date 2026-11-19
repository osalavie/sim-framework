using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<List<TenantInfo>> GetTenantsAsync() =>
        SendJsonAsync<List<TenantInfo>>(HttpMethod.Get, "api/system/tenant");

    public Task<TenantCreateResult> CreateTenantAsync(TenantCreate input) =>
        SendJsonAsync<TenantCreateResult>(HttpMethod.Post, "api/system/tenant", input);

    public Task<TenantInfo> UpdateTenantAsync(Guid id, TenantUpdate input) =>
        SendJsonAsync<TenantInfo>(HttpMethod.Put, $"api/system/tenant/{id:D}", input);

    public async Task DeleteTenantAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/system/tenant/{id:D}");
    }

    public Task<TenantTemplates> GetTenantTemplatesAsync() =>
        SendJsonAsync<TenantTemplates>(HttpMethod.Get, "api/system/tenant/templates");

    public Task<TenantAssignments> GetTenantAssignmentsAsync(Guid id) =>
        SendJsonAsync<TenantAssignments>(HttpMethod.Get, $"api/system/tenant/{id:D}/assignments");
}

public sealed class TenantInfo
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public string? AdminEmail { get; set; }
    public string? ConnectionString { get; set; }
    public DateTime? CreationTime { get; set; }
}

public sealed class TenantCreate
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? AdminEmail { get; set; }
    public string? ConnectionString { get; set; }
    public string? AdminUserName { get; set; }
    public string? AdminPassword { get; set; }
    public List<Guid> ModuleIds { get; set; } = [];
    public List<Guid> RouteIds { get; set; } = [];
    public List<Guid> PermissionIds { get; set; } = [];
}

public sealed class TenantUpdate
{
    public string? Name { get; set; }
    public string? AdminEmail { get; set; }
    public string? ConnectionString { get; set; }
}

public sealed class TenantCreateResult
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantCode { get; set; }
    public Guid AdminUserId { get; set; }
    public string? AdminUserName { get; set; }
    public string? InitialPassword { get; set; }
    public int AssignedModuleCount { get; set; }
    public int AssignedRouteCount { get; set; }
    public int AssignedPermissionCount { get; set; }
}

public sealed class TenantTemplates
{
    public List<TenantModuleTemplate> Modules { get; set; } = [];
    public List<TenantRouteTemplate> Routes { get; set; } = [];
    public List<TenantPermissionTemplate> Permissions { get; set; } = [];
}

public sealed class TenantAssignments
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public List<TenantModuleTemplate> Modules { get; set; } = [];
    public List<TenantRouteTemplate> Routes { get; set; } = [];
    public List<TenantPermissionTemplate> Permissions { get; set; } = [];
}

public sealed class TenantModuleTemplate
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; }
}

public sealed class TenantRouteTemplate
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Title { get; set; }
    public bool Menu { get; set; }
    public int Order { get; set; }
}

public sealed class TenantPermissionTemplate
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}
