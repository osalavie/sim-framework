using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.User;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared;

namespace Sim.Security.Tests;

public sealed class ExternalLoginProviderTests
{
    [Fact]
    public void Registry_should_only_expose_enabled_providers()
    {
        var registry = new ExternalLoginProviderRegistry([
            new FakeProvider("google", true),
            new FakeProvider("microsoft", false)
        ]);

        var provider = registry.EnabledProviders.Single();
        Assert.Equal("google", provider.Id);
        Assert.Same(provider, registry.Get("GOOGLE"));
        Assert.Throws<BusinessException>(() => registry.Get("microsoft"));
    }

    [Fact]
    public void Registry_should_reject_unknown_provider()
    {
        var registry = new ExternalLoginProviderRegistry([]);
        Assert.Throws<BusinessException>(() => registry.Get("removed-provider"));
    }

    [Fact]
    public async Task WeChat_redirect_should_encode_protocol_parameters()
    {
        var provider = CreateWeChatProvider();

        var url = await provider.BuildRedirectUrlAsync(
            new("https://localhost/login?provider=wechat&state=state%2F1", "state/1", "nonce"));

        Assert.StartsWith("https://open.weixin.qq.com/connect/qrconnect?", url);
        Assert.Contains("appid=app%20id", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%2Flogin%3Fprovider%3Dwechat%26state%3Dstate%252F1", url);
        Assert.Contains("scope=snsapi_login", url);
        Assert.Contains("state=state%2F1", url);
        Assert.EndsWith("#wechat_redirect", url);
    }

    [Fact]
    public async Task QQ_redirect_should_encode_protocol_parameters()
    {
        var provider = CreateQQProvider();

        var url = await provider.BuildRedirectUrlAsync(
            new("https://localhost/login?provider=qq&state=state%2F1", "state/1", "nonce"));

        Assert.StartsWith("https://graph.qq.com/oauth2.0/authorize?", url);
        Assert.Contains("client_id=client%20id", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%2Flogin%3Fprovider%3Dqq%26state%3Dstate%252F1", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("state=state%2F1", url);
    }

    [Fact]
    public async Task Steam_redirect_should_use_openid_2_protocol()
    {
        var provider = new SteamExternalLoginProvider(
            Options.Create(new ExternalLoginOptions
            {
                Steam = new() { Enabled = true, Realm = "https://example.com" }
            }),
            new FakeHttpClientFactory(new ThrowingHandler()));

        var url = await provider.BuildRedirectUrlAsync(
            new("https://localhost/auth/external/callback", "steam.state-token", "nonce"));

        Assert.StartsWith("https://steamcommunity.com/openid/login?", url);
        Assert.Contains("openid.mode=checkid_setup", url);
        Assert.Contains("openid.return_to=https%3A%2F%2Flocalhost%2Fauth%2Fexternal%2Fcallback%3Fstate%3Dsteam.state-token", url);
        Assert.Contains("openid.realm=https%3A%2F%2Fexample.com", url);
        Assert.Contains("openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select", url);
    }

    [Fact]
    public void Provider_key_hash_should_be_stable_and_provider_scoped()
    {
        var first = ExternalLoginSecurity.CreateProviderKeyHash(ExternalLoginProviders.Steam, "76561197960287930");
        var second = ExternalLoginSecurity.CreateProviderKeyHash(ExternalLoginProviders.Steam, "76561197960287930");
        var other = ExternalLoginSecurity.CreateProviderKeyHash(ExternalLoginProviders.QQ, "76561197960287930");

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public async Task WeChat_should_prefer_unionid_as_stable_provider_key()
    {
        var provider = new WeChatExternalLoginProvider(
            Options.Create(new ExternalLoginOptions
            {
                WeChat = new() { Enabled = true, ClientId = "app id", ClientSecret = "secret" }
            }),
            new FakeHttpClientFactory(new JsonHandler("""
                {"openid":"openid","unionid":"unionid","nickname":"Alice"}
                """)));

        var identity = await provider.AuthenticateAsync(new(
            new ExternalLoginCallbackRequest { Code = "code" },
            "https://localhost/login?provider=wechat&state=state",
            "nonce",
            new Dictionary<string, string>()));

        Assert.Equal("unionid", identity.ProviderKey);
        Assert.Equal("Alice", identity.UserName);
        Assert.Equal("Alice", identity.DisplayName);
    }

    private static WeChatExternalLoginProvider CreateWeChatProvider() =>
        new(
            Options.Create(new ExternalLoginOptions
            {
                WeChat = new() { Enabled = true, ClientId = "app id", ClientSecret = "secret" }
            }),
            new FakeHttpClientFactory(new ThrowingHandler()));

    private static QQExternalLoginProvider CreateQQProvider() =>
        new(
            Options.Create(new ExternalLoginOptions
            {
                QQ = new() { Enabled = true, ClientId = "client id", ClientSecret = "secret" }
            }),
            new FakeHttpClientFactory(new ThrowingHandler()));

    private sealed record FakeProvider(string Id, bool Enabled) : IExternalLoginProvider
    {
        public string DisplayName => Id;

        public Task<string> BuildRedirectUrlAsync(
            ExternalLoginRedirectContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ExternalLoginIdentity> AuthenticateAsync(
            ExternalLoginAuthenticationContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
