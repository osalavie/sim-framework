namespace Sim.Framework.Core.Configuration;

/// <summary>
/// Swagger配置选项
/// </summary>
public class SwaggerOptions
{
    /// <summary>
    /// Swagger文档列表
    /// </summary>
    public List<SwaggerDocumentOptions> Documents { get; set; } = new();
}
