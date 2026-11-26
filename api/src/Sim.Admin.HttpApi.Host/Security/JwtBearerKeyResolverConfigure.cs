using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Security;

namespace Sim.Admin.HttpApi.Host.Security;

/// <summary>
/// Sets the <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/>
/// on JwtBearer options to support kid-based key lookup for seamless key rotation.
/// </summary>
internal sealed class JwtBearerKeyResolverConfigure : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSigningKeyStore _keyStore;

    public JwtBearerKeyResolverConfigure(JwtSigningKeyStore keyStore)
    {
        _keyStore = keyStore;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == "sim_admin_api")
        {
            options.TokenValidationParameters.IssuerSigningKeyResolver =
                (token, securityToken, kid, validationParameters) =>
                    _keyStore.ResolveSigningKeys(kid);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        Configure(Options.DefaultName, options);
    }
}
