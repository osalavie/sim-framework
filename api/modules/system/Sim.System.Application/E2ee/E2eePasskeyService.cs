using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.E2ee;
using Sim.System.Application.Security;
using Sim.System.Domain.Shared.ErrorCodes;
using SqlSugar;
using Sim.System.Domain.E2ee;

namespace Sim.System.Application.E2ee;

[Authorize]
public sealed class E2eePasskeyService(
    ISqlSugarRepository<UserE2eePasskeyEntity> passkeys,
    ISqlSugarRepository<UserE2eePasskeyChallengeEntity> challenges,
    ISqlSugarRepository<UserE2eeIdentityEntity> identities,
    ISqlSugarClient database,
    E2eePasskeyWebAuthn webAuthn) : ApplicationService, IE2eePasskeyService
{
    private Guid UserId => CurrentUser.Id ?? throw new AuthorizationException();
    private static string Scope => UserE2eeIdentityEntity.ToTenantScope(TenantContext.CurrentTenantId);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static BusinessException Invalid() => new(SystemErrorCodes.E2eePasskeyInvalid);

    /// <summary>
    /// 获取 E2EE 通行密钥功能配置。
    /// </summary>
    public Task<E2eePasskeyConfigurationDto> GetConfigurationAsync() => Task.FromResult(
        new E2eePasskeyConfigurationDto { Enabled = webAuthn.Enabled, RpId = webAuthn.Enabled ? webAuthn.RpId : "" });

    /// <summary>
    /// 获取当前用户的 E2EE 通行密钥列表。
    /// </summary>
    public async Task<List<E2eePasskeyDto>> GetListAsync()
    {
        var owner = UserId;
        var tenantScope = Scope;
        var identity = await IdentityAsync();
        var rows = await passkeys.GetListAsync(row => row.UserId == owner && row.TenantScope == tenantScope && row.KeyVersion == identity.KeyVersion);
        return rows.Select(Map).ToList();
    }

    /// <summary>
    /// 创建通行密钥注册选项（WebAuthn）。
    /// </summary>
    [HttpPost, RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "e2ee-passkey-register")]
    public async Task<E2eePasskeyOptionsDto> PostRegistrationOptionsAsync()
    {
        RequireEnabled();
        var identity = await IdentityAsync();
        var owner = UserId;
        var tenantScope = Scope;
        var existing = await passkeys.GetListAsync(row => row.UserId == owner && row.TenantScope == tenantScope);
        if (existing.Count >= 10) throw new BusinessException(SystemErrorCodes.E2eePasskeyLimit);
        var options = webAuthn.Create().RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = Handle(owner), Name = owner.ToString("N"), DisplayName = CurrentUser.UserName ?? "E2EE" },
            ExcludeCredentials = existing.Select(row => new PublicKeyCredentialDescriptor(Decode(row.CredentialId))).ToList(),
            AuthenticatorSelection = new AuthenticatorSelection { ResidentKey = ResidentKeyRequirement.Preferred, UserVerification = UserVerificationRequirement.Required },
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs { PRF = new AuthenticationExtensionsPRFInputs() }
        });
        return await SaveChallengeAsync("register", identity.KeyVersion, options.ToJson(), Encode(RandomNumberGenerator.GetBytes(32)));
    }

    /// <summary>
    /// 完成通行密钥注册。
    /// </summary>
    [HttpPost, RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "e2ee-passkey-register")]
    public async Task<E2eePasskeyDto> PostRegistrationAsync(CompleteE2eePasskeyRegistrationDto input)
    {
        RequireEnabled();
        var challenge = await ConsumeAsync(input.ChallengeId, "register");
        try
        {
            ValidateCredential(input.Credential);
            if (!IsWrap(input.WrappedUmk) || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100) throw Invalid();
            var raw = input.Credential.Deserialize<AuthenticatorAttestationRawResponse>(JsonOptions) ?? throw Invalid();
            var credential = await webAuthn.Create().MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = raw, OriginalOptions = CredentialCreateOptions.FromJson(challenge.OptionsJson),
                IsCredentialIdUniqueToUserCallback = async (args, _) =>
                {
                    var hash = Hash(args.CredentialId);
                    return !await passkeys.GetQueryable().Where(row => row.CredentialHash == hash && row.TenantScope == challenge.TenantScope).AnyAsync();
                }
            });
            var owner = UserId;
        var tenantScope = Scope;
            var existing = await passkeys.GetListAsync(row => row.UserId == owner && row.TenantScope == tenantScope);
            if (existing.Count >= 10)
                throw new BusinessException(SystemErrorCodes.E2eePasskeyLimit);
            var name = input.Name.Trim();
            // Leave space for the short code without splitting a UTF-16 surrogate pair.
            if (name.Length > 90) name = name[..(char.IsHighSurrogate(name[89]) ? 89 : 90)];
            var id = Guid.NewGuid();
            var displayName = $"{name} · {id.ToString("N")[..4].ToUpperInvariant()}";
            while (existing.Any(item => string.Equals(item.Name, displayName, StringComparison.OrdinalIgnoreCase)))
            {
                id = Guid.NewGuid();
                displayName = $"{name} · {id.ToString("N")[..4].ToUpperInvariant()}";
            }
            var row = new UserE2eePasskeyEntity
            {
                Id = id, UserId = owner, CreatorId = owner, TenantId = TenantContext.CurrentTenantId, TenantScope = Scope,
                CredentialId = Encode(credential.Id), CredentialHash = Hash(credential.Id), PublicKey = Encode(credential.PublicKey),
                UserHandle = Encode(credential.User.Id), SignCount = credential.SignCount, IsBackupEligible = credential.IsBackupEligible,
                Name = displayName, RpId = webAuthn.RpId, KeyVersion = challenge.KeyVersion,
                PrfSalt = challenge.PrfSalt, WrappedUmk = input.WrappedUmk, CreationTime = DateTime.UtcNow
            };
            await passkeys.InsertAsync(row);
            Logger.LogInformation("E2EE passkey registered. UserId={UserId} PasskeyId={PasskeyId}", owner, row.Id);
            return Map(row);
        }
        catch (BusinessException) { throw; }
        catch { throw Invalid(); }
    }

    /// <summary>
    /// 创建指定通行密钥的解锁选项（WebAuthn）。
    /// </summary>
    [HttpPost, RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "e2ee-passkey-unlock")]
    public async Task<E2eePasskeyOptionsDto> PostUnlockOptionsAsync(Guid id)
    {
        RequireEnabled();
        var key = await OwnedPasskeyAsync(id);
        var options = webAuthn.Create().GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [new PublicKeyCredentialDescriptor(Decode(key.CredentialId))],
            UserVerification = UserVerificationRequirement.Required,
            Extensions = new AuthenticationExtensionsClientInputs
            {
                PRF = new AuthenticationExtensionsPRFInputs { Eval = new AuthenticationExtensionsPRFValues { First = Decode(key.PrfSalt) } }
            }
        });
        return await SaveChallengeAsync("unlock", key.KeyVersion, options.ToJson(), key.PrfSalt, key.Id);
    }

    /// <summary>
    /// 验证通行密钥并返回加密主密钥（UMK）封装。
    /// </summary>
    [HttpPost, RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "e2ee-passkey-unlock")]
    public async Task<E2eePasskeyWrapDto> PostUnlockAsync(CompleteE2eePasskeyUnlockDto input)
    {
        RequireEnabled();
        var challenge = await ConsumeAsync(input.ChallengeId, "unlock");
        try
        {
            ValidateCredential(input.Credential);
            var key = await OwnedPasskeyAsync(challenge.PasskeyId ?? throw Invalid());
            var raw = input.Credential.Deserialize<AuthenticatorAssertionRawResponse>(JsonOptions) ?? throw Invalid();
            if (Encode(raw.RawId) != key.CredentialId) throw Invalid();
            var result = await webAuthn.Create().MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = raw, OriginalOptions = AssertionOptions.FromJson(challenge.OptionsJson),
                StoredPublicKey = Decode(key.PublicKey), StoredSignatureCounter = checked((uint)key.SignCount),
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    Encode(args.CredentialId) == key.CredentialId && Encode(args.UserHandle) == key.UserHandle)
            });
            if (raw.Response.AuthenticatorData.Length < 37 ||
                ((raw.Response.AuthenticatorData[32] & 0x08) != 0) != key.IsBackupEligible) throw Invalid();
            var scope = Scope;
            var owner = UserId;
        var tenantScope = Scope;
            // Only audit/counter fields change; never overwrite a concurrently removed credential.
            var changed = await database.Updateable<UserE2eePasskeyEntity>()
                .SetColumns(row => new UserE2eePasskeyEntity { SignCount = result.SignCount, LastUsedAt = DateTime.UtcNow })
                .Where(row => row.Id == key.Id && row.UserId == owner && row.TenantScope == tenantScope && row.TenantScope == scope && row.SignCount == key.SignCount)
                .ExecuteCommandAsync();
            if (changed != 1) throw Invalid();
            return new E2eePasskeyWrapDto { CredentialId = key.CredentialId, RpId = key.RpId,
                PrfSalt = key.PrfSalt, WrappedUmk = key.WrappedUmk, KeyVersion = key.KeyVersion };
        }
        catch (BusinessException) { throw; }
        catch { throw Invalid(); }
    }

    /// <summary>
    /// 重命名指定的通行密钥。
    /// </summary>
    [HttpPut]
    public async Task<E2eePasskeyDto> PutNameAsync(Guid id, RenameE2eePasskeyDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100) throw Invalid();
        var key = await OwnedPasskeyAsync(id);
        var owner = UserId;
        var tenantScope = Scope;
        var name = input.Name.Trim();
        var modified = DateTime.UtcNow;
        // Update only the label/audit fields; do not write stale counters or key material back.
        var changed = await database.Updateable<UserE2eePasskeyEntity>()
            .SetColumns(row => new UserE2eePasskeyEntity { Name = name, LastModificationTime = modified, LastModifierId = owner })
            .Where(row => row.Id == id && row.UserId == owner && row.TenantScope == tenantScope && row.KeyVersion == key.KeyVersion)
            .ExecuteCommandAsync();
        if (changed != 1) throw Invalid();
        key.Name = name;
        key.LastModificationTime = modified;
        key.LastModifierId = owner;
        return Map(key);
    }

    /// <summary>
    /// 删除指定的通行密钥。
    /// </summary>
    [HttpDelete]
    public async Task DeleteAsync(Guid id)
    {
        var owner = UserId;
        var tenantScope = Scope;
        // Deletion remains available even after the feature/configuration is disabled.
        await passkeys.DeleteAsync(row => row.Id == id && row.UserId == owner && row.TenantScope == tenantScope);
        Logger.LogInformation("E2EE passkey removal requested. UserId={UserId} PasskeyId={PasskeyId}", owner, id);
    }

    private async Task<UserE2eeIdentityEntity> IdentityAsync()
    {
        var owner = UserId;
        var tenantScope = Scope;
        return await identities.GetAsync(row => row.UserId == owner && row.TenantScope == tenantScope && row.IsActive) ?? throw Invalid();
    }

    private async Task<UserE2eePasskeyEntity> OwnedPasskeyAsync(Guid id)
    {
        var owner = UserId;
        var tenantScope = Scope;
        var identity = await IdentityAsync();
        return await passkeys.GetAsync(row => row.Id == id && row.UserId == owner && row.TenantScope == tenantScope && row.KeyVersion == identity.KeyVersion && row.RpId == webAuthn.RpId) ?? throw Invalid();
    }

    private async Task<E2eePasskeyOptionsDto> SaveChallengeAsync(string operation, int version, string json, string salt, Guid? passkeyId = null)
    {
        var owner = UserId;
        var tenantScope = Scope;
        var now = DateTime.UtcNow;
        await challenges.DeleteAsync(row => row.UserId == owner && row.TenantScope == tenantScope && row.ExpiresAt < now);
        var row = new UserE2eePasskeyChallengeEntity { Id = Guid.NewGuid(), UserId = owner, CreatorId = owner,
            TenantId = TenantContext.CurrentTenantId, TenantScope = Scope, Operation = operation, OptionsJson = json,
            PrfSalt = salt, PasskeyId = passkeyId, KeyVersion = version, ExpiresAt = now.AddMinutes(5) };
        await challenges.InsertAsync(row);
        return new E2eePasskeyOptionsDto { ChallengeId = row.Id, KeyVersion = version, PrfSalt = salt, PublicKey = JsonDocument.Parse(json).RootElement.Clone() };
    }

    private async Task<UserE2eePasskeyChallengeEntity> ConsumeAsync(Guid id, string operation)
    {
        var owner = UserId;
        var tenantScope = Scope;
        var scope = Scope;
        var identity = await IdentityAsync();
        var now = DateTime.UtcNow;
        var row = await challenges.GetAsync(item => item.Id == id && item.UserId == owner && item.TenantScope == tenantScope && item.Operation == operation &&
            item.KeyVersion == identity.KeyVersion && !item.IsConsumed && item.ExpiresAt > now) ?? throw Invalid();
        // No enclosing rollback: failed verification also consumes this challenge.
        var changed = await database.Updateable<UserE2eePasskeyChallengeEntity>().SetColumns(item => item.IsConsumed == true)
            .Where(item => item.Id == id && item.UserId == owner && item.TenantScope == tenantScope && item.TenantScope == scope && !item.IsConsumed).ExecuteCommandAsync();
        if (changed != 1) throw Invalid();
        return row;
    }

    private void RequireEnabled() { if (!webAuthn.Enabled) throw new BusinessException(SystemErrorCodes.E2eePasskeyUnavailable); }
    private static byte[] Handle(Guid owner) => SHA256.HashData(Encoding.UTF8.GetBytes(Scope + ":" + owner.ToString("N")));
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
    private static bool IsWrap(string value)
    {
        try { return value.StartsWith("passkey:v1:", StringComparison.Ordinal) && value.Length <= 128 &&
            value[11..].All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') && Decode(value[11..]).Length == 44; }
        catch { return false; }
    }
    private static void ValidateCredential(JsonElement credential)
    {
        // PRF outputs must stay in the client. Only explicit public credential fields are accepted.
        if (credential.ValueKind != JsonValueKind.Object || credential.GetRawText().Length > 65536 ||
            credential.EnumerateObject().Any(property => property.Name is not ("id" or "rawId" or "type" or "response"))) throw Invalid();
        if (!credential.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object ||
            response.EnumerateObject().Any(property => property.Name is not ("clientDataJSON" or "attestationObject" or "transports" or "authenticatorData" or "signature" or "userHandle"))) throw Invalid();
    }
    private static E2eePasskeyDto Map(UserE2eePasskeyEntity row) => new() { Id = row.Id, Name = row.Name, RpId = row.RpId,
        KeyVersion = row.KeyVersion, CreationTime = row.CreationTime, CreatorId = row.CreatorId,
        LastModificationTime = row.LastModificationTime, LastModifierId = row.LastModifierId, LastUsedAt = row.LastUsedAt };
}
