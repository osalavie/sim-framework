using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sim.Framework.Domain.Security;
using Sim.System.Domain.Security;
using Sim.System.Domain.User;

namespace Sim.Security.Tests;

public class Sm2JwtTests
{
    [Fact]
    public void Sm2SignedTokenValidatesWithKidAndCorrectAlg()
    {
        var gm = new GmCryptoService();
        var (publicKey, privateKey) = gm.GenerateSm2KeyPair();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "tests",
            ["JwtSettings:Audience"] = "tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["JwtSettings:SigningKeys:0:Kid"] = "sm2-1",
            ["JwtSettings:SigningKeys:0:Algorithm"] = GmCryptoAlgorithms.Sm2WithSm3,
            ["JwtSettings:SigningKeys:0:PublicKey"] = publicKey,
            ["JwtSettings:SigningKeys:0:PrivateKey"] = privateKey,
            ["JwtSettings:SigningKeys:0:IsActive"] = "true"
        });
        var keyStore = new JwtSigningKeyStore(configuration);
        var manager = CreateManager(configuration, keyStore);

        var token = manager.GenerateAccessToken([
            new Claim("sub", "u1"),
            new Claim("role", "admin")
        ]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal(GmCryptoAlgorithms.Sm2WithSm3, jwt.Header.Alg);
        Assert.Equal("sm2-1", jwt.Header.Kid);

        var principal = handler.ValidateToken(token, CreateValidationParameters(keyStore), out _);
        Assert.Equal("u1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("admin", principal.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void NestedSigningKeysFromKeyPerFileAreBound()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "tests",
            ["JwtSettings:Audience"] = "tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["JwtSettings:SigningKeys:Keys:0:Kid"] = "1",
            ["JwtSettings:SigningKeys:Keys:0:Secret"] = "test-hmac-secret-at-least-32-bytes-2026",
            ["JwtSettings:SigningKeys:Keys:0:IsActive"] = "true"
        });
        var keyStore = new JwtSigningKeyStore(configuration);

        Assert.True(keyStore.IsMultiKeyMode);
        Assert.Equal("1", keyStore.GetActiveKid());

        var manager = CreateManager(configuration, keyStore);
        var token = manager.GenerateAccessToken([new Claim("sub", "u1")]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("1", jwt.Header.Kid);
    }

    [Fact]
    public void TamperedSm2TokenFailsValidation()
    {
        var gm = new GmCryptoService();
        var (publicKey, privateKey) = gm.GenerateSm2KeyPair();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "tests",
            ["JwtSettings:Audience"] = "tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            ["JwtSettings:SigningKeys:0:Kid"] = "sm2-1",
            ["JwtSettings:SigningKeys:0:Algorithm"] = GmCryptoAlgorithms.Sm2WithSm3,
            ["JwtSettings:SigningKeys:0:PublicKey"] = publicKey,
            ["JwtSettings:SigningKeys:0:PrivateKey"] = privateKey,
            ["JwtSettings:SigningKeys:0:IsActive"] = "true"
        });
        var keyStore = new JwtSigningKeyStore(configuration);
        var manager = CreateManager(configuration, keyStore);

        var token = manager.GenerateAccessToken([new Claim("sub", "u1")]);
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(tampered, CreateValidationParameters(keyStore), out _));
    }

    [Fact]
    public void HmacActiveKeyStillSignsAndValidatesWithRetiredSm2Key()
    {
        var gm = new GmCryptoService();
        var (publicKey, _) = gm.GenerateSm2KeyPair();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = "tests",
            ["JwtSettings:Audience"] = "tests",
            ["JwtSettings:AccessTokenExpirationMinutes"] = "10",
            // Active HMAC key
            ["JwtSettings:SigningKeys:0:Kid"] = "hmac-1",
            ["JwtSettings:SigningKeys:0:Secret"] = "test-hmac-secret-at-least-32-bytes-2026",
            ["JwtSettings:SigningKeys:0:IsActive"] = "true",
            // Retired SM2 key kept for validation only (no private key)
            ["JwtSettings:SigningKeys:1:Kid"] = "sm2-retired",
            ["JwtSettings:SigningKeys:1:Algorithm"] = GmCryptoAlgorithms.Sm2WithSm3,
            ["JwtSettings:SigningKeys:1:PublicKey"] = publicKey,
            ["JwtSettings:SigningKeys:1:IsActive"] = "false"
        });
        var keyStore = new JwtSigningKeyStore(configuration);
        var manager = CreateManager(configuration, keyStore);

        var token = manager.GenerateAccessToken([new Claim("sub", "u1")]);

        var handler = new JwtSecurityTokenHandler();
        Assert.Equal(SecurityAlgorithms.HmacSha256, handler.ReadJwtToken(token).Header.Alg);
        var principal = handler.ValidateToken(token, CreateValidationParameters(keyStore), out _);
        Assert.Equal("u1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        // 旧 SM2 密钥仍可被解析用于校验
        Assert.Contains(keyStore.ResolveSigningKeys("sm2-retired"), key => key is Sm2SecurityKey);
    }

    private static SecurityManager CreateManager(IConfiguration configuration, JwtSigningKeyStore keyStore)
    {
        var securityOptions = Options.Create(new SecurityOptions());
        var passwordHasher = new PasswordHasher<UserEntity>(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 100_000
        }));
        return new SecurityManager(securityOptions, configuration, passwordHasher, keyStore);
    }

    private static TokenValidationParameters CreateValidationParameters(JwtSigningKeyStore keyStore)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "tests",
            ValidateAudience = true,
            ValidAudience = "tests",
            ValidateLifetime = false,
            IssuerSigningKeyResolver = (_, _, kid, _) => keyStore.ResolveSigningKeys(kid)
        };
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}