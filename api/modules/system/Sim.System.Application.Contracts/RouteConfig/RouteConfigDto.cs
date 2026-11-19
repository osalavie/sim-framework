using Sim.System.Application.Contracts.RouteMeta;
using System.Text.Json.Serialization;
using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.RouteConfig;

/// <summary>
/// Route configuration DTO (corresponds to frontend BackendRoute interface)
/// </summary>
public class RouteConfigDto : AuditedEntityDto<Guid>
{
    /// <summary>
    /// Route identifier
    /// </summary>
    [JsonPropertyName("id")]
    public new Guid Id { get; set; }
    
    /// <summary>
    /// Associated module IDs (many-to-many)
    /// </summary>
    [JsonPropertyName("moduleIds")]
    public List<Guid> ModuleIds { get; set; } = [];
    
    /// <summary>
    /// Associated module names joined for display purposes
    /// </summary>
    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; set; }
    
    /// <summary>
    /// Parent route ID (for hierarchical structure)
    /// </summary>
    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Route path
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Route name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Component path (relative to views or modules directory)
    /// </summary>
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Route metadata
    /// </summary>
    [JsonPropertyName("meta")]
    public RouteMetaDto? Meta { get; set; }
    
    /// <summary>
    /// Child routes
    /// </summary>
    [JsonPropertyName("children")]
    public List<RouteConfigDto>? Children { get; set; }
    
    /// <summary>
    /// Redirect path
    /// </summary>
    [JsonPropertyName("redirect")]
    public string? Redirect { get; set; }
}
