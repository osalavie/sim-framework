namespace Sim.Framework.Core.Configuration;

/// <summary>
/// Swagger文档配置选项
/// </summary>
public class SwaggerDocumentOptions
{
    /// <summary>
    /// 文档名称（用于路由和标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 文档标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// Swagger JSON URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 文档描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否聚合全部模块接口：设为 true 时该文档不再按 <c>api/{Name}/</c> 前缀过滤，
    /// 而是包含宿主暴露的全部业务接口（<c>api/**</c>，不含 Swagger 自身的基础设施接口），
    /// 便于一次性导出并导入 Apifox 等接口工具。
    /// </summary>
    public bool IncludeAll { get; set; }
}
