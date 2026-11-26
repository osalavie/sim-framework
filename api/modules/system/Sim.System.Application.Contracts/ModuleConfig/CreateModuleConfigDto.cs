using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sim.System.Application.Contracts.Validation;

namespace Sim.System.Application.Contracts.Dtos;

/// <summary>
/// DTO for creating a new module configuration
/// </summary>
public class CreateModuleConfigDto
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
    /// Whether the module is enabled (default: true)
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Module icon (optional, max 128 characters)
    /// </summary>
    [StringLength(128)]
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display order (default: 0)
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;
    
    /// <summary>
    /// Layout type: "admin" | "custom" | "none" (required, default: "admin")
    /// </summary>
    [Required]
    [ValidLayoutType]
    [JsonPropertyName("layout")]
    public string Layout { get; set; } = "admin";
    
    /// <summary>
    /// Empty redirect path (optional, max 256 characters)
    /// </summary>
    [StringLength(256)]
    [JsonPropertyName("emptyRedirect")]
    public string? EmptyRedirect { get; set; }

    /// <summary>
    /// 初始关联的路由 ID；为 null 时不建立任何关联。
    /// </summary>
    [JsonPropertyName("routeIds")]
    public List<Guid>? RouteIds { get; set; }
}
