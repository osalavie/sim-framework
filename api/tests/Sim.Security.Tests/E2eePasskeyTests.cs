using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.SqlSugar.Core.Migrations;
using Sim.System.Application.Contracts.E2ee;
using Sim.System.Application.Security;
using SqlSugar;
using Sim.System.Application.E2ee;
using Sim.System.Domain.E2ee;

namespace Sim.Security.Tests;

[Collection(HostCollection.Name)]
public sealed class E2eePasskeyTests(HostFixture fixture) : IClassFixture<HostFixture>
{
    private const string Rp = "example.test";
    private const string Origin = "https://example.test";
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static E2eePasskeyWebAuthn Configuration(string origin = Origin) => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["E2eePasskeys:Enabled"] = "true", ["E2eePasskeys:RpId"] = Rp,
            ["E2eePasskeys:Origins:0"] = origin }).Build());

    private async Task<(AsyncServiceScope Scope, E2eePasskeyService Service)> CreateAsync()
    {
        var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>().MigrateAsync();
        var owner = new Current(Guid.NewGuid());
        var identity = scope.ServiceProvider.GetRequiredService<E2eeService>();
        identity.CurrentUser = owner;
        await identity.PostIdentityAsync(new CreateUserE2eeIdentityDto { KdfAlgorithm = "PBKDF2-SM3", KdfSalt = Encode(new byte[16]),
            KdfIterations = 600_000, PasswordWrappedKey = "wrap:v1:" + Encode(new byte[44]), RecoveryWrappedKey = "wrap:v1:" + Encode(new byte[44]) });
        var service = ActivatorUtilities.CreateInstance<E2eePasskeyService>(scope.ServiceProvider, Configuration());
        service.CurrentUser = owner;
        return (scope, service);
    }

    private static byte[] ClientData(E2eePasskeyOptionsDto options, string type, string origin = Origin, string? challenge = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new { type, challenge = challenge ?? options.PublicKey.GetProperty("challenge").GetString(), origin, crossOrigin = false });

    private static byte[] AuthenticatorData(byte flags, uint count, string rp = Rp)
    {
        var data = new byte[37];
        SHA256.HashData(Encoding.UTF8.GetBytes(rp)).CopyTo(data, 0);
        data[32] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(33), count);
        return data;
    }

    private sealed class Authenticator : IDisposable
    {
        public readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public readonly byte[] Id = RandomNumberGenerator.GetBytes(32);
        public string? Handle;

        public JsonElement Registration(E2eePasskeyOptionsDto options)
        {
            Handle = options.PublicKey.GetProperty("user").GetProperty("id").GetString();
            var parameters = Key.ExportParameters(false);
            var cose = new CborWriter();
            cose.WriteStartMap(5);
            cose.WriteInt32(1); cose.WriteInt32(2);
            cose.WriteInt32(3); cose.WriteInt32(-7);
            cose.WriteInt32(-1); cose.WriteInt32(1);
            cose.WriteInt32(-2); cose.WriteByteString(parameters.Q.X!);
            cose.WriteInt32(-3); cose.WriteByteString(parameters.Q.Y!);
            cose.WriteEndMap();
            var prefix = AuthenticatorData(0x45, 0);
            var suffix = new byte[18 + Id.Length];
            BinaryPrimitives.WriteUInt16BigEndian(suffix.AsSpan(16), (ushort)Id.Length);
            Id.CopyTo(suffix, 18);
            var authData = prefix.Concat(suffix).Concat(cose.Encode()).ToArray();
            var attestation = new CborWriter();
            attestation.WriteStartMap(3);
            attestation.WriteTextString("fmt"); attestation.WriteTextString("none");
            attestation.WriteTextString("attStmt"); attestation.WriteStartMap(0); attestation.WriteEndMap();
            attestation.WriteTextString("authData"); attestation.WriteByteString(authData);
            attestation.WriteEndMap();
            return Json(new { id = Encode(Id), rawId = Encode(Id), type = "public-key", response = new {
                clientDataJSON = Encode(ClientData(options, "webauthn.create")), attestationObject = Encode(attestation.Encode()), transports = new[] { "internal" } } });
        }

        public JsonElement Assertion(E2eePasskeyOptionsDto options, string change = "", uint count = 1)
        {
            var authData = AuthenticatorData(change == "uv" ? (byte)1 : (byte)5, count, change == "rp" ? "other.test" : Rp);
            var client = ClientData(options, "webauthn.get", change == "origin" ? "https://evil.test" : Origin,
                change == "challenge" ? Encode(new byte[32]) : null);
            var signed = authData.Concat(SHA256.HashData(client)).ToArray();
            var signature = Key.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            if (change == "signature") signature[^1] ^= 1;
            var id = change == "credential" ? new byte[32] : Id;
            return Json(new { id = Encode(id), rawId = Encode(id), type = "public-key", response = new {
                clientDataJSON = Encode(client), authenticatorData = Encode(authData), signature = Encode(signature),
                userHandle = change == "handle" ? Encode(new byte[32]) : Handle } });
        }
        public void Dispose() => Key.Dispose();
    }

    private static async Task<E2eePasskeyDto> RegisterAsync(E2eePasskeyService service, Authenticator authenticator)
    {
        var options = await service.PostRegistrationOptionsAsync();
        Assert.Equal("required", options.PublicKey.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString());
        return await service.PostRegistrationAsync(new CompleteE2eePasskeyRegistrationDto { ChallengeId = options.ChallengeId,
            Name = "My passkey", Credential = authenticator.Registration(options), WrappedUmk = "passkey:v1:" + Encode(RandomNumberGenerator.GetBytes(44)) });
    }

    [Fact]
    public async Task Http_endpoints_require_an_authenticated_account()
    {
        using var client = fixture.CreateHttpsClient();
        foreach (var path in new[] { "/api/system/e2ee-passkey/configuration", "/api/system/e2ee-passkey" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(global::System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var registration = await client.PostAsync("/api/system/e2ee-passkey/registration-options", null);
        Assert.Equal(global::System.Net.HttpStatusCode.Unauthorized, registration.StatusCode);
        using var rename = await client.PutAsync($"/api/system/e2ee-passkey/{Guid.NewGuid()}/name",
            new StringContent("{\"name\":\"Office\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(global::System.Net.HttpStatusCode.Unauthorized, rename.StatusCode);
    }

    [Fact]
    public async Task Names_have_short_codes_and_renaming_only_changes_owned_label_and_audit()
    {
        var (scope, service) = await CreateAsync();
        await using var cleanup = scope;
        using var authenticator = new Authenticator();
        var registered = await RegisterAsync(service, authenticator);
        Assert.Matches("^My passkey · [0-9A-F]{4}$", registered.Name);
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var before = await database.Queryable<UserE2eePasskeyEntity>().InSingleAsync(registered.Id);
        var renamed = await service.PutNameAsync(registered.Id, new() { Name = "  办公电脑 · A7F2  " });
        Assert.Equal("办公电脑 · A7F2", renamed.Name);
        Assert.Equal(service.CurrentUser.Id, renamed.LastModifierId);
        Assert.NotNull(renamed.LastModificationTime);
        var after = await database.Queryable<UserE2eePasskeyEntity>().InSingleAsync(registered.Id);
        Assert.Equal(before.PublicKey, after.PublicKey);
        Assert.Equal(before.WrappedUmk, after.WrappedUmk);
        Assert.Equal(before.PrfSalt, after.PrfSalt);
        Assert.Equal(before.SignCount, after.SignCount);
        Assert.Equal(before.LastUsedAt, after.LastUsedAt);
        await Assert.ThrowsAsync<BusinessException>(() => service.PutNameAsync(registered.Id, new() { Name = "  " }));
        await Assert.ThrowsAsync<BusinessException>(() => service.PutNameAsync(registered.Id, new() { Name = new string('a', 101) }));
        var owner = service.CurrentUser;
        service.CurrentUser = new Current(Guid.NewGuid());
        await Assert.ThrowsAsync<BusinessException>(() => service.PutNameAsync(registered.Id, new() { Name = "Other" }));
        service.CurrentUser = owner;
        var previous = Sim.Framework.Domain.TenantContext.CurrentTenantId;
        Sim.Framework.Domain.TenantContext.CurrentTenantId = Guid.NewGuid();
        try { await Assert.ThrowsAsync<BusinessException>(() => service.PutNameAsync(registered.Id, new() { Name = "Other" })); }
        finally { Sim.Framework.Domain.TenantContext.CurrentTenantId = previous; }
        Assert.Equal(renamed.Name, (await service.GetListAsync()).Single().Name);
        var options = await service.PostUnlockOptionsAsync(registered.Id);
        var wrap = await service.PostUnlockAsync(new() { ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options) });
        Assert.Equal(before.WrappedUmk, wrap.WrappedUmk);
    }

    [Fact]
    public async Task Registers_and_verifies_a_real_es256_assertion_and_rejects_replay()
    {
        var (scope, service) = await CreateAsync();
        await using var cleanup = scope;
        using var authenticator = new Authenticator();
        var registered = await RegisterAsync(service, authenticator);
        Assert.Single(await service.GetListAsync());
        var options = await service.PostUnlockOptionsAsync(registered.Id);
        Assert.Equal("required", options.PublicKey.GetProperty("userVerification").GetString());
        var request = new CompleteE2eePasskeyUnlockDto { ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options) };
        var wrap = await service.PostUnlockAsync(request);
        Assert.Equal(Encode(authenticator.Id), wrap.CredentialId);
        Assert.StartsWith("passkey:v1:", wrap.WrappedUmk);
        Assert.Equal(1, wrap.KeyVersion);
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(request));
        await service.DeleteAsync(registered.Id);
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockOptionsAsync(registered.Id));
        Assert.Empty(await service.GetListAsync());
    }

    [Theory]
    [InlineData("uv")]
    [InlineData("rp")]
    [InlineData("origin")]
    [InlineData("challenge")]
    [InlineData("signature")]
    [InlineData("credential")]
    [InlineData("handle")]
    public async Task Invalid_assertions_are_rejected_and_consume_the_challenge(string change)
    {
        var (scope, service) = await CreateAsync();
        await using var cleanup = scope;
        using var authenticator = new Authenticator();
        var registered = await RegisterAsync(service, authenticator);
        var options = await service.PostUnlockOptionsAsync(registered.Id);
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(new() {
            ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options, change) }));
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(new() {
            ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options) }));
    }

    [Fact]
    public async Task Expired_and_foreign_challenges_and_prf_output_payloads_are_rejected()
    {
        var (scope, service) = await CreateAsync();
        await using var cleanup = scope;
        using var authenticator = new Authenticator();
        var registered = await RegisterAsync(service, authenticator);
        var options = await service.PostUnlockOptionsAsync(registered.Id);
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        await database.Updateable<UserE2eePasskeyChallengeEntity>().SetColumns(row => row.ExpiresAt == DateTime.UtcNow.AddMinutes(-1))
            .Where(row => row.Id == options.ChallengeId).ExecuteCommandAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(new() {
            ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options) }));
        var original = service.CurrentUser;
        service.CurrentUser = new Current(Guid.NewGuid());
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockOptionsAsync(registered.Id));
        await service.DeleteAsync(registered.Id);
        service.CurrentUser = original;
        Assert.Single(await service.GetListAsync());
        options = await service.PostUnlockOptionsAsync(registered.Id);
        var payload = Json(new { id = Encode(authenticator.Id), rawId = Encode(authenticator.Id), type = "public-key", clientExtensionResults = new { prf = new { results = new { first = "secret" } } } });
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(new() { ChallengeId = options.ChallengeId, Credential = payload }));
    }

    [Theory]
    [InlineData("https://evil.test")]
    [InlineData("http://example.test")]
    [InlineData("https://*.example.test")]
    public void Configuration_rejects_unrelated_insecure_or_wildcard_origins(string origin) => Assert.False(Configuration(origin).Enabled);

    [Fact]
    public async Task Tenant_scope_and_identity_reset_invalidate_old_credentials_and_challenges()
    {
        var (scope, service) = await CreateAsync();
        await using var cleanup = scope;
        using var authenticator = new Authenticator();
        var registered = await RegisterAsync(service, authenticator);
        var options = await service.PostUnlockOptionsAsync(registered.Id);
        var previous = Sim.Framework.Domain.TenantContext.CurrentTenantId;
        Sim.Framework.Domain.TenantContext.CurrentTenantId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockOptionsAsync(registered.Id));
            await service.DeleteAsync(registered.Id);
        }
        finally { Sim.Framework.Domain.TenantContext.CurrentTenantId = previous; }
        Assert.Single(await service.GetListAsync());
        var identity = scope.ServiceProvider.GetRequiredService<E2eeService>();
        identity.CurrentUser = service.CurrentUser;
        await identity.ResetIdentityAsync(new ResetUserE2eeIdentityDto { KdfAlgorithm = "PBKDF2-SM3", KdfSalt = Encode(new byte[16]),
            KdfIterations = 600_000, PasswordWrappedKey = "wrap:v1:" + Encode(new byte[44]), RecoveryWrappedKey = "wrap:v1:" + Encode(new byte[44]) });
        Assert.Empty(await service.GetListAsync());
        await Assert.ThrowsAsync<BusinessException>(() => service.PostUnlockAsync(new() { ChallengeId = options.ChallengeId, Credential = authenticator.Assertion(options) }));
        var database = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        Assert.False(await database.Queryable<UserE2eePasskeyEntity>().Where(row => row.Id == registered.Id).AnyAsync());
    }

    private sealed class Current(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id => id;
        public string? UserName => "passkey-test";
        public string? Name => null;
        public string? SurName => null;
        public string? Email => null;
        public bool EmailVerified => false;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => [];
        public Claim? FindClaim(string claimType) => null;
        public string? FindClaimValue(string claimType) => null;
        public Claim[] FindClaims(string claimType) => [];
        public Claim[] GetAllClaims() => [];
        public bool IsInRole(string roleName) => false;
    }
}
