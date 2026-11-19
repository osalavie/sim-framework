using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Sim.System.Application.Security;

public interface IOidcConfigurationManagerFactory
{
    Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string authority,
        CancellationToken cancellationToken = default);
}

public sealed class OidcConfigurationManagerFactory : IOidcConfigurationManagerFactory
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> managers = new(StringComparer.OrdinalIgnoreCase);

    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string authority,
        CancellationToken cancellationToken = default)
    {
        var manager = managers.GetOrAdd(
            authority.TrimEnd('/'),
            value => new ConfigurationManager<OpenIdConnectConfiguration>(
                value + "/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true }));
        return manager.GetConfigurationAsync(cancellationToken);
    }
}
