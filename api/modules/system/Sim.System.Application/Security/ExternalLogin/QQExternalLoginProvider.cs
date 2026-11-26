using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Security;

public sealed class QQExternalLoginProvider(
    IOptions<ExternalLoginOptions> options,
    IHttpClientFactory httpClientFactory) : IExternalLoginProvider
{
    private const string AuthorizeEndpoint = "https://graph.qq.com/oauth2.0/authorize";
    private const string TokenEndpoint = "https://graph.qq.com/oauth2.0/token";
    private const string OpenIdEndpoint = "https://graph.qq.com/oauth2.0/me";
    private const string UserInfoEndpoint = "https://graph.qq.com/user/get_user_info";

    public string Id => ExternalLoginProviders.QQ;

    public string DisplayName => ExternalLoginDisplayNames.QQ;

    public bool Enabled => options.Value.QQ.Enabled;

    public Task<string> BuildRedirectUrlAsync(
        ExternalLoginRedirectContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = options.Value.QQ;
        return Task.FromResult(
            AuthorizeEndpoint +
            "?response_type=code" +
            $"&client_id={Uri.EscapeDataString(provider.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(context.CallbackUrl)}" +
            $"&state={Uri.EscapeDataString(context.State)}");
    }

    public async Task<ExternalLoginIdentity> AuthenticateAsync(
        ExternalLoginAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Code))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var provider = options.Value.QQ;
        using var client = httpClientFactory.CreateClient("ExternalLoginProvider");

        using var tokenResponse = await client.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = provider.ClientId,
                ["client_secret"] = provider.ClientSecret,
                ["code"] = context.Request.Code,
                ["redirect_uri"] = context.CallbackUrl,
                ["fmt"] = "json"
            }),
            cancellationToken);
        var token = await tokenResponse.ReadRequiredJsonAsync<QQTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        using var openIdResponse = await client.GetAsync(
            OpenIdEndpoint +
            $"?access_token={Uri.EscapeDataString(token.AccessToken)}&fmt=json",
            cancellationToken);
        var openIdPayload = await openIdResponse.ReadRequiredJsonAsync<QQOpenIdResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(openIdPayload.OpenId))
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        using var userResponse = await client.GetAsync(
            UserInfoEndpoint +
            $"?access_token={Uri.EscapeDataString(token.AccessToken)}" +
            $"&oauth_consumer_key={Uri.EscapeDataString(provider.ClientId)}" +
            $"&openid={Uri.EscapeDataString(openIdPayload.OpenId)}",
            cancellationToken);
        var userPayload = await userResponse.ReadRequiredJsonAsync<QQUserInfoResponse>(cancellationToken);
        return new ExternalLoginIdentity(openIdPayload.OpenId, userPayload.NickName, userPayload.NickName);
    }

    private sealed class QQTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class QQOpenIdResponse
    {
        [JsonPropertyName("openid")]
        public string? OpenId { get; init; }
    }

    private sealed class QQUserInfoResponse
    {
        [JsonPropertyName("nickname")]
        public string? NickName { get; init; }
    }
}
