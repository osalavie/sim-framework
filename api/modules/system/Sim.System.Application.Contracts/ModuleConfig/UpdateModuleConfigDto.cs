using Sim.System.Application.Contracts.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.Dtos;

/// <summary>
/// DTO for updating an existing module configuration
/// </summary>
public class UpdateModuleConfigDto: EntityDto<Guid>
{
    /// <summary>
    /// Module name (required, 1-256 characters)
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Module description (optional, max 512 characters)
    /// </summary>
    [StringLength(512)]
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Module version (optional, max 32 characters)
    /// </summary>
    [StringLength(32)]
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    
    /// <summary>
    /// Whether the module is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Module icon (optional, max 128 characters)
    /// </summary>
    [StringLength(128)]
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }
    
    /// <summary>
    /// Layout type: "admin" | "custom" | "none" (required)
    /// </summary>
    [Required]
    [ValidLayoutType]
    [JsonPropertyName("layout")]
    public string Layout { get; set; } = string.Empty;
    
    /// <summary>
    /// Empty redirect path (optional, max 256 characters)
    /// </summary>
    [StringLength(256)]
    [JsonPropertyName("emptyRedirect")]
    public string? EmptyRedirect { get; set; }

    /// <summary>
    /// 覆盖该模块关联的路由 ID；为 null 时保持现状。
    /// </summary>
    [JsonPropertyName("routeIds")]
    public List<Guid>? RouteIds { get; set; }
}
