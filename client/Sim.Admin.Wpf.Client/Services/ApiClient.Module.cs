using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<ApiModuleDetail> GetModuleAsync(Guid id) =>
        SendJsonAsync<ApiModuleDetail>(HttpMethod.Get, $"api/system/module-config/{id:D}");

    public Task<PagedResult<ApiRouteOption>> GetRouteOptionsAsync() =>
        SendJsonAsync<PagedResult<ApiRouteOption>>(HttpMethod.Get,
            "api/system/route-config?skipCount=0&maxResultCount=500");

    public async Task UpdateModuleRoutesAsync(ApiModuleDetail module, IEnumerable<Guid> routeIds)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/system/module-config/{module.Id:D}", new
        {
            name = module.Name,
            description = module.Description,
            enabled = module.Enabled,
            icon = module.Icon,
            order = module.Order,
            layout = module.Layout,
            emptyRedirect = module.EmptyRedirect,
            routeIds = routeIds.Select(id => id.ToString()).ToArray()
        });
    }
}

public sealed class ApiModuleDetail
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public string? Icon { get; set; }
    public int Order { get; set; }
    public string? Layout { get; set; }
    public string? EmptyRedirect { get; set; }
    public List<string> RouteIds { get; set; } = [];
}

public sealed class ApiRouteOption
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public Guid? ParentId { get; set; }
}
