using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sim.System.Application.Contracts.RouteMeta;
using Sim.System.Application.Contracts.Validation;

namespace Sim.System.Application.Contracts.RouteConfig;

/// <summary>
/// DTO for creating a new route configuration
/// </summary>
public class CreateRouteConfigDto
{
    /// <summary>
    /// Parent route ID (optional)
    /// </summary>
    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; set; }
    
    /// <summary>
    /// Route name (required, 1-256 characters)
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Route path (required, 1-512 characters, optional leading "/", supports :param and :param? segments)
    /// </summary>
    [Required]
    [StringLength(512, MinimumLength = 1)]
    [ValidRoutePath]
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Component path (required, 1-512 characters)
    /// </summary>
    [Required]
    [StringLength(512, MinimumLength = 1)]
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Redirect path (optional, max 512 characters)
    /// </summary>
    [StringLength(512)]
    [JsonPropertyName("redirect")]
    public string? Redirect { get; set; }
    
    /// <summary>
    /// Route metadata (optional)
    /// </summary>
    [JsonPropertyName("meta")]
    public CreateRouteMetaDto? Meta { get; set; }
}
