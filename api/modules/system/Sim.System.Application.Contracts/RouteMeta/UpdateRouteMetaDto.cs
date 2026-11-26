using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sim.System.Application.Contracts.RouteMeta;

/// <summary>
/// DTO for updating route metadata
/// </summary>
public class UpdateRouteMetaDto
{
    /// <summary>
    /// Page title (optional, max 256 characters)
    /// </summary>
    [StringLength(256)]
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    /// <summary>
    /// Icon name (optional, max 128 characters)
    /// </summary>
    [StringLength(128)]
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Whether to show in menu
    /// </summary>
    [JsonPropertyName("menu")]
    public bool Menu { get; set; }
    
    /// <summary>
    /// Whether to support tab
    /// </summary>
    [JsonPropertyName("tab")]
    public bool Tab { get; set; }
    
    /// <summary>
    /// Whether to keep alive
    /// </summary>
    [JsonPropertyName("keepAlive")]
    public bool KeepAlive { get; set; }
    
    /// <summary>
    /// Whether to pin the tab
    /// </summary>
    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; }
    
    /// <summary>
    /// Whether to allow anonymous access
    /// </summary>
    [JsonPropertyName("anonymous")]
    public bool Anonymous { get; set; }
    
    /// <summary>
    /// Whether the route is disabled
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }
    
    /// <summary>
    /// Module identifier (optional, max 128 characters)
    /// </summary>
    [StringLength(128)]
    [JsonPropertyName("module")]
    public string? Module { get; set; }

    /// <summary>
    /// 访问该路由所需的权限码；多个权限码用逗号分隔。
    /// </summary>
    [StringLength(512)]
    [JsonPropertyName("permissionCode")]
    public string? PermissionCode { get; set; }

    /// <summary>
    /// 多个权限码是否必须全部满足；false（默认）表示任一满足即可。
    /// </summary>
    [JsonPropertyName("permissionRequireAll")]
    public bool PermissionRequireAll { get; set; } = false;
}
