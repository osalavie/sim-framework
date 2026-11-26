using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.Framework.Domain.Shared.Dependency;

namespace Sim.Framework.Domain.Services;

/// <summary>
/// Domain service marker interface. Replaces Volo.Abp.Domain.Services.IDomainService.
/// Implements ITransientDependency so subclasses are auto-registered as Transient.
/// </summary>
public interface IDomainService : ITransientDependency;

/// <summary>
/// Domain service base class providing ILogger access. Replaces Volo.Abp.Domain.Services.DomainService.
/// Subclasses that extend DomainService are auto-registered as Transient via ITransientDependency convention.
/// </summary>
public abstract class DomainService : IDomainService
{
    public ILogger Logger { get; set; } = NullLogger.Instance;

    protected DomainService() { }
}
