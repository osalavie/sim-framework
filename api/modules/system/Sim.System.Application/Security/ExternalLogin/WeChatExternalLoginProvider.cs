using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Security;

public sealed class WeChatExternalLoginProvider(
    IOptions<ExternalLoginOptions> options,
    IHttpClientFactory httpClientFactory) : IExternalLoginProvider
{
    private const string AuthorizeEndpoint = "https://open.weixin.qq.com/connect/qrconnect";
    private const string TokenEndpoint = "https://api.weixin.qq.com/sns/oauth2/access_token";

    public string Id => ExternalLoginProviders.WeChat;

    public string DisplayName => ExternalLoginDisplayNames.WeChat;

    public bool Enabled => options.Value.WeChat.Enabled;

    public Task<string> BuildRedirectUrlAsync(
        ExternalLoginRedirectContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = options.Value.WeChat;
        return Task.FromResult(
            AuthorizeEndpoint +
            $"?appid={Uri.EscapeDataString(provider.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(context.CallbackUrl)}" +
            "&response_type=code" +
            "&scope=snsapi_login" +
            $"&state={Uri.EscapeDataString(context.State)}" +
            "#wechat_redirect");
    }

    public async Task<ExternalLoginIdentity> AuthenticateAsync(
        ExternalLoginAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Code))
        {
            throw ExternalLoginErrors.InvalidIdentity();
        }

        var provider = options.Value.WeChat;
        var url =
            TokenEndpoint +
            $"?appid={Uri.EscapeDataString(provider.ClientId)}" +
            $"&secret={Uri.EscapeDataString(provider.ClientSecret)}" +
            $"&code={Uri.EscapeDataString(context.Request.Code)}" +
            "&grant_type=authorization_code";

        using var client = httpClientFactory.CreateClient("ExternalLoginProvider");
        using var response = await client.GetAsync(url, cancellationToken);
        var token = await response.ReadRequiredJsonAsync<WeChatTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token.OpenId))
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        var providerKey = string.IsNullOrWhiteSpace(token.UnionId) ? token.OpenId : token.UnionId;
        return new ExternalLoginIdentity(providerKey, token.NickName, token.NickName);
    }

    private sealed class WeChatTokenResponse
    {
        [JsonPropertyName("openid")]
        public string? OpenId { get; init; }

        [JsonPropertyName("unionid")]
        public string? UnionId { get; init; }

        [JsonPropertyName("nickname")]
        public string? NickName { get; init; }
    }
}
