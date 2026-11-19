using System.Text.Json.Serialization;
using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.Dtos;

/// <summary>
/// Module configuration DTO (corresponds to frontend ModuleInfo interface)
/// </summary>
public class ModuleConfigDto: AuditedEntityDto<Guid>
{
    /// <summary>
    /// Module ID (Guid converted to string for frontend)
    /// </summary>
    [JsonPropertyName("id")]
    public new string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Module name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Module description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Whether the module is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Module icon
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; set; }
    
    /// <summary>
    /// Layout type: "admin" | "custom" | "none"
    /// </summary>
    [JsonPropertyName("layout")]
    public string? Layout { get; set; }
    
    /// <summary>
    /// Empty redirect path
    /// </summary>
    [JsonPropertyName("emptyRedirect")]
    public string? EmptyRedirect { get; set; }

    /// <summary>
    /// 该模块下发/展示的路由 ID 集合。
    /// </summary>
    [JsonPropertyName("routeIds")]
    public List<Guid> RouteIds { get; set; } = [];
}
