using System.Reflection;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 常规控制器配置。记录需要自动暴露为 API 控制器的应用服务程序集
/// 及其路由根路径，替代 ABP 的 ConventionalControllers 配置。
/// </summary>
public sealed class ConventionalControllerOptions
{
    /// <summary>未配置模块的默认路由根路径。</summary>
    public const string DefaultRootPath = "app";

    private readonly Dictionary<Assembly, string> _moduleRootPaths = new();

    public IReadOnlyDictionary<Assembly, string> ModuleRootPaths => _moduleRootPaths;

    public void AddModule(Assembly assembly, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _moduleRootPaths[assembly] = rootPath;
    }

    public string GetRootPath(Assembly assembly)
    {
        return _moduleRootPaths.TryGetValue(assembly, out var rootPath)
            ? rootPath
            : DefaultRootPath;
    }
}
