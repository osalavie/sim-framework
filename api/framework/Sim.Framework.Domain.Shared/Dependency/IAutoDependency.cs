namespace Sim.Framework.Domain.Shared.Dependency;

/// <summary>
/// 自动依赖注入扫描根标记。实现此接口的类会被自动扫描并注册到 DI 容器。
/// 不直接实现此接口，请使用 <see cref="ITransientDependency"/>、<see cref="IScopedDependency"/> 或 <see cref="ISingletonDependency"/>。
/// </summary>
public interface IAutoDependency
{
}
