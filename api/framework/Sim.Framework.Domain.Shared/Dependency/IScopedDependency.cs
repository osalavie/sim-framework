namespace Sim.Framework.Domain.Shared.Dependency;

/// <summary>
/// 标记为 Scoped 生命周期。同一请求内共享同一实例。
/// 适用场景：持有 DB 上下文的 Repository、需要请求级状态的应用服务。
/// </summary>
public interface IScopedDependency : IAutoDependency
{
}
