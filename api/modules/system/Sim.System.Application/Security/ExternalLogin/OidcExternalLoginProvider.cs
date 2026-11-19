using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Security;

public abstract class OidcExternalLoginProvider(
    IOidcConfigurationManagerFactory configurationManagerFactory,
    IHttpClientFactory httpClientFactory) : IExternalLoginProvider
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public abstract OidcExternalLoginProviderOptions ProviderOptions { get; }

    public bool Enabled => ProviderOptions.Enabled;

    public async Task<string> BuildRedirectUrlAsync(
        ExternalLoginRedirectContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = ProviderOptions;
        var configuration = await configurationManagerFactory.GetConfigurationAsync(
            provider.Authority,
            cancellationToken);
        var scopes = provider.Scopes.Contains("openid")
            ? provider.Scopes
            : ["openid", .. provider.Scopes];
        var query =
            $"client_id={Uri.EscapeDataString(provider.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(context.CallbackUrl)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(' ', scopes))}" +
            $"&state={Uri.EscapeDataString(context.State)}" +
            $"&nonce={Uri.EscapeDataString(context.Nonce)}";
        return configuration.AuthorizationEndpoint +
            (configuration.AuthorizationEndpoint.Contains('?') ? "&" : "?") +
            query;
    }

    public async Task<ExternalLoginIdentity> AuthenticateAsync(
        ExternalLoginAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Code))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var provider = ProviderOptions;
        var configuration = await configurationManagerFactory.GetConfigurationAsync(
            provider.Authority,
            cancellationToken);

        using var client = httpClientFactory.CreateClient("ExternalLoginProvider");
        using var tokenResponse = await client.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = provider.ClientId,
                ["client_secret"] = provider.ClientSecret,
                ["code"] = context.Request.Code,
                ["redirect_uri"] = context.CallbackUrl
            }),
            cancellationToken);
        var tokenPayload = await tokenResponse.ReadRequiredJsonAsync<OidcTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenPayload.IdToken))
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        var principal = await ValidateIdToken(
            tokenPayload.IdToken,
            configuration,
            provider.ClientId,
            context.Nonce);
        var subject = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var userName = principal.FindFirstValue("preferred_username") ??
            principal.FindFirstValue("email");
        var displayName = principal.FindFirstValue("name") ?? userName;
        return new ExternalLoginIdentity(
            subject,
            userName,
            displayName,
            principal.FindFirstValue("email"));
    }

    private static async Task<ClaimsPrincipal> ValidateIdToken(
        string idToken,
        OpenIdConnectConfiguration configuration,
        string clientId,
        string nonce)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            idToken,
            new TokenValidationParameters
            {
                ValidIssuer = ResolveExpectedIssuer(idToken, configuration),
                ValidAudience = clientId,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                RequireAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            });
        if (!result.IsValid ||
            result.ClaimsIdentity is null ||
            !string.Equals(result.ClaimsIdentity.FindFirst("nonce")?.Value, nonce, StringComparison.Ordinal))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    private static string ResolveExpectedIssuer(
        string idToken,
        OpenIdConnectConfiguration configuration)
    {
        var expectedIssuer = configuration.Issuer;
        if (string.IsNullOrWhiteSpace(expectedIssuer))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        if (!expectedIssuer.Contains("{tenantid}", StringComparison.OrdinalIgnoreCase))
        {
            return expectedIssuer;
        }

        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(idToken))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var token = handler.ReadJsonWebToken(idToken);
        var tenantId = token.Claims.FirstOrDefault(claim => claim.Type == "tid")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        return expectedIssuer.Replace(
            "{tenantid}",
            Uri.EscapeDataString(tenantId),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OidcTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}

public sealed class MicrosoftExternalLoginProvider(
    IOptions<ExternalLoginOptions> options,
    IOidcConfigurationManagerFactory configurationManagerFactory,
    IHttpClientFactory httpClientFactory)
    : OidcExternalLoginProvider(configurationManagerFactory, httpClientFactory)
{
    public override string Id => ExternalLoginProviders.Microsoft;

    public override string DisplayName => ExternalLoginDisplayNames.Microsoft;

    public override OidcExternalLoginProviderOptions ProviderOptions => options.Value.Microsoft;
}

public sealed class GoogleExternalLoginProvider(
    IOptions<ExternalLoginOptions> options,
    IOidcConfigurationManagerFactory configurationManagerFactory,
    IHttpClientFactory httpClientFactory)
    : OidcExternalLoginProvider(configurationManagerFactory, httpClientFactory)
{
    public override string Id => ExternalLoginProviders.Google;

    public override string DisplayName => ExternalLoginDisplayNames.Google;

    public override OidcExternalLoginProviderOptions ProviderOptions => options.Value.Google;
}
