namespace Sim.Framework.Domain.Shared.Dependency;

/// <summary>
/// 标记为 Transient 生命周期。每次注入创建新实例。
/// 适用场景：无状态的领域服务（DomainService）、工具类。
/// </summary>
public interface ITransientDependency : IAutoDependency
{
}
