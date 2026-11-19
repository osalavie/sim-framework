using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.Framework.Application.Mapping;
using Sim.Framework.Domain.Shared.Security;

namespace Sim.Framework.Application.Services;

/// <summary>
/// Base class for application services. Replaces Volo.Abp.Application.Services.ApplicationService.
/// Provides Logger, CurrentUser, and ObjectMapper access.
/// </summary>
public abstract class ApplicationService
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    public IObjectMapper ObjectMapper { get; set; } = NullObjectMapper.Instance;
    public ICurrentUser CurrentUser { get; set; } = NullCurrentUser.Instance;

    protected ApplicationService() { }
}
