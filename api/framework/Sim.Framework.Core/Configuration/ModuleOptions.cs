namespace Sim.Framework.Core.Configuration;

/// <summary>
/// 模块配置选项
/// </summary>
public class ModuleOptions
{
    /// <summary>
    /// 模块列表
    /// </summary>
    public List<ModuleInfo> Modules { get; set; } = new();
}

/// <summary>
/// 模块信息
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// 模块名称（用于路由前缀）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模块程序集全名
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// 模块类型全名（包含命名空间）
    /// </summary>
    public string TypeName { get; set; } = string.Empty;
}
