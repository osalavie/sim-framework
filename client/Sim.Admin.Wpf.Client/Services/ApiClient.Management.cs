using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    /// <summary>按接口地址下载内容文件（带鉴权，供头像预览使用）。</summary>
    public async Task<byte[]> DownloadContentFileAsync(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? absolute.PathAndQuery.TrimStart('/')
            : url.TrimStart('/');
        using var response = await SendRawAsync(HttpMethod.Get, path);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task ResetUserPasswordAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/system/user/{id:D}/reset-user-password");
    }

    public Task<ApiUserDetail> GetUserAsync(Guid id) =>
        SendJsonAsync<ApiUserDetail>(HttpMethod.Get, $"api/system/user/{id:D}");

    public Task<PagedResult<ApiRole>> GetRolesAsync() =>
        SendJsonAsync<PagedResult<ApiRole>>(HttpMethod.Get,
            "api/system/role?skipCount=0&maxResultCount=500");

    public async Task UpdateUserRolesAsync(ApiUserDetail user, IEnumerable<Guid> roleIds)
    {
        using var response = await SendAsync(HttpMethod.Put, "api/system/user", new
        {
            id = user.Id,
            userName = user.UserName,
            nickName = string.IsNullOrWhiteSpace(user.NickName) ? user.UserName : user.NickName,
            avatar = user.Avatar,
            name = user.Name,
            sex = user.Sex,
            phone = user.Phone,
            email = user.Email,
            address = user.Address,
            description = user.Description,
            roleIds = roleIds.Select(id => id.ToString()).ToArray()
        });
    }

    public async Task EnableTenantAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/system/tenant/{id:D}/enable");
    }

    public async Task DisableTenantAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/system/tenant/{id:D}/disable");
    }
}

public sealed class ApiUserDetail
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Name { get; set; }
    public string? Avatar { get; set; }
    public int? Sex { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    public List<ApiRole> Roles { get; set; } = [];
}

public sealed class ApiRole
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}
