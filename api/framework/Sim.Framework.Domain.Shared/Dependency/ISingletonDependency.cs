namespace Sim.Framework.Domain.Shared.Dependency;

/// <summary>
/// 标记为 Singleton 生命周期。整个应用生命周期共享同一实例。
/// 适用场景：缓存管理、配置读取器、无状态基础设施组件。
/// </summary>
public interface ISingletonDependency : IAutoDependency
{
}
