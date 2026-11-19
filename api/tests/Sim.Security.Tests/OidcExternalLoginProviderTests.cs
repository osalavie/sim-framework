using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.User;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared;

namespace Sim.Security.Tests;

public sealed class OidcExternalLoginProviderTests
{
    [Fact]
    public async Task Google_should_exchange_code_and_validate_signed_id_token_with_nonce()
    {
        using var rsa = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(rsa);
        var configuration = new OpenIdConnectConfiguration
        {
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            Issuer = "https://accounts.google.com"
        };
        configuration.SigningKeys.Add(securityKey);
        var idToken = CreateIdToken(securityKey, configuration.Issuer!, "client-id", "nonce-value");
        var provider = CreateGoogleProvider(configuration, idToken);

        var identity = await provider.AuthenticateAsync(new(
            new ExternalLoginCallbackRequest { Code = "code" },
            "https://localhost/login?provider=google&state=state",
            "nonce-value",
            new Dictionary<string, string>()));

        Assert.Equal("subject-1", identity.ProviderKey);
        Assert.Equal("alice@example.com", identity.UserName);
        Assert.Equal("Alice", identity.DisplayName);
        Assert.Equal("alice@example.com", identity.Email);
    }

    [Fact]
    public async Task Google_should_reject_id_token_with_wrong_nonce()
    {
        using var rsa = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(rsa);
        var configuration = new OpenIdConnectConfiguration
        {
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            Issuer = "https://accounts.google.com"
        };
        configuration.SigningKeys.Add(securityKey);
        var idToken = CreateIdToken(securityKey, configuration.Issuer!, "client-id", "other-nonce");
        var provider = CreateGoogleProvider(configuration, idToken);

        await Assert.ThrowsAsync<BusinessException>(() => provider.AuthenticateAsync(new(
            new ExternalLoginCallbackRequest { Code = "code" },
            "https://localhost/login?provider=google&state=state",
            "nonce-value",
            new Dictionary<string, string>())));
    }

    [Fact]
    public async Task Microsoft_common_authority_should_validate_tenant_specific_issuer()
    {
        using var rsa = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(rsa);
        var configuration = new OpenIdConnectConfiguration
        {
            AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            Issuer = "https://login.microsoftonline.com/{tenantid}/v2.0"
        };
        configuration.SigningKeys.Add(securityKey);
        var issuer = "https://login.microsoftonline.com/tenant-1/v2.0";
        var idToken = CreateIdToken(
            securityKey,
            issuer,
            "client-id",
            "nonce-value",
            tenantId: "tenant-1");
        var provider = CreateMicrosoftProvider(configuration, idToken);

        var identity = await provider.AuthenticateAsync(new(
            new ExternalLoginCallbackRequest { Code = "code" },
            "https://localhost/login?provider=microsoft&state=state",
            "nonce-value",
            new Dictionary<string, string>()));

        Assert.Equal("subject-1", identity.ProviderKey);
    }

    private static GoogleExternalLoginProvider CreateGoogleProvider(
        OpenIdConnectConfiguration configuration,
        string idToken) =>
        new(
            Options.Create(new ExternalLoginOptions
            {
                Google = new()
                {
                    Enabled = true,
                    Authority = "https://accounts.google.com",
                    ClientId = "client-id",
                    ClientSecret = "secret"
                }
            }),
            new FixedConfigurationManagerFactory(configuration),
            new FakeHttpClientFactory(new TokenJsonHandler(idToken)));

    private static MicrosoftExternalLoginProvider CreateMicrosoftProvider(
        OpenIdConnectConfiguration configuration,
        string idToken) =>
        new(
            Options.Create(new ExternalLoginOptions
            {
                Microsoft = new()
                {
                    Enabled = true,
                    Authority = "https://login.microsoftonline.com/common/v2.0",
                    ClientId = "client-id",
                    ClientSecret = "secret"
                }
            }),
            new FixedConfigurationManagerFactory(configuration),
            new FakeHttpClientFactory(new TokenJsonHandler(idToken)));

    private static string CreateIdToken(
        RsaSecurityKey securityKey,
        string issuer,
        string audience,
        string nonce,
        string? tenantId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", "subject-1"),
            new("name", "Alice"),
            new("email", "alice@example.com"),
            new("nonce", nonce)
        };
        if (tenantId is not null)
        {
            claims.Add(new("tid", tenantId));
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-1).UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)
        });
    }

    private sealed class FixedConfigurationManagerFactory(OpenIdConnectConfiguration configuration)
        : IOidcConfigurationManagerFactory
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string authority,
            CancellationToken cancellationToken = default) => Task.FromResult(configuration);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class TokenJsonHandler(string idToken) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = $$"""{"id_token":"{{idToken}}"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
