using Microsoft.Extensions.Options;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Security;

public sealed class SteamExternalLoginProvider(
    IOptions<ExternalLoginOptions> options,
    IHttpClientFactory httpClientFactory) : IExternalLoginProvider
{
    private const string Endpoint = "https://steamcommunity.com/openid/login";

    public string Id => ExternalLoginProviders.Steam;

    public string DisplayName => ExternalLoginDisplayNames.Steam;

    public bool Enabled => options.Value.Steam.Enabled;

    public Task<string> BuildRedirectUrlAsync(
        ExternalLoginRedirectContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authority = new Uri(context.CallbackUrl, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
        var realm = string.IsNullOrWhiteSpace(options.Value.Steam.Realm)
            ? authority
            : options.Value.Steam.Realm;
        var returnTo = context.CallbackUrl +
            (context.CallbackUrl.Contains("?") ? "&" : "?") +
            "state=" + Uri.EscapeDataString(context.State);
        return Task.FromResult(
            Endpoint +
            "?openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0" +
            "&openid.mode=checkid_setup" +
            $"&openid.return_to={Uri.EscapeDataString(returnTo)}" +
            $"&openid.realm={Uri.EscapeDataString(realm)}" +
            "&openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select" +
            "&openid.claimed_id=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select");
    }

    public async Task<ExternalLoginIdentity> AuthenticateAsync(
        ExternalLoginAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        var claimedId = context.Query.GetValueOrDefault("openid.claimed_id");
        if (string.IsNullOrWhiteSpace(claimedId))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var verification = new Dictionary<string, string>(context.Query, StringComparer.OrdinalIgnoreCase)
        {
            ["openid.mode"] = "check_authentication"
        };

        using var client = httpClientFactory.CreateClient("ExternalLoginProvider");
        using var response = await client.PostAsync(
            Endpoint,
            new FormUrlEncodedContent(verification),
            cancellationToken);
        var body = await response.ReadRequiredTextAsync(cancellationToken);
        if (!body.Contains("is_valid:true", StringComparison.Ordinal))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        const string prefix = "https://steamcommunity.com/openid/id/";
        if (!claimedId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !long.TryParse(claimedId[prefix.Length..].TrimEnd('/'), out var steamId))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        return new ExternalLoginIdentity(
            steamId.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            null,
            $"Steam {steamId}");
    }
}
