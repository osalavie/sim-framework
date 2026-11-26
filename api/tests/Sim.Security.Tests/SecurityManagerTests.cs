using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Security;
using Sim.System.Domain.Security;
using Sim.System.Domain.Shared.Security;
using Sim.System.Domain.User;

namespace Sim.Security.Tests;

public class SecurityManagerTests
{
    [Fact]
    public void PasswordHasherRoundTripSucceedsAndRejectsWrongPassword()
    {
        var manager = CreateManager();
        var encrypted = manager.EncryptPassword("Correct#Password1");
        var user = new UserEntity
        {
            EncryptedPassword = encrypted.EncryptedPassword,
            Salt = encrypted.Salt
        };

        Assert.Empty(encrypted.Salt);
        Assert.True(manager.VerifyPassword(user, "Correct#Password1"));
        Assert.False(manager.VerifyPassword(user, "Wrong#Password1"));
    }

    [Fact]
    public void WeakJwtKeyIsRejected()
    {
        var manager = CreateManager("short-key");

        var exception = Assert.Throws<InvalidOperationException>(() => manager.GenerateAccessToken([]));

        Assert.Contains("No JWT signing key is configured", exception.Message);
    }

    [Fact]
    public async Task PasswordPolicyReportsMissingCharacterClasses()
    {
        var result = await CreateManager().ValidatePasswordAsync("lowercaseonly");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(8, result.MinLength);
    }

    private static SecurityManager CreateManager(
        string signingKey = "test-signing-key-with-at-least-32-bytes-2026")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:IssuerSigningKey"] = signingKey,
                ["JwtSettings:Issuer"] = "tests",
                ["JwtSettings:Audience"] = "tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "10"
            })
            .Build();
        var passwordHasher = new PasswordHasher<UserEntity>(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 100_000
        }));
        var keyStore = new JwtSigningKeyStore(configuration);

        return new SecurityManager(
            new StubPasswordPolicyProvider(), configuration, passwordHasher, keyStore);
    }

    private sealed class StubPasswordPolicyProvider : IPasswordPolicyProvider
    {
        public Task<PasswordPolicy> GetAsync() => Task.FromResult(
            new PasswordPolicy(8, true, true, true, true));
    }
}
