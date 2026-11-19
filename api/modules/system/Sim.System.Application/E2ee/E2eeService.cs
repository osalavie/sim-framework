using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.E2ee;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.E2ee;

namespace Sim.System.Application.E2ee;

/// <summary>
/// 当前登录用户的 E2EE 身份。接口只做格式与所有权校验，不验证加密口令、不解密任何数据；
/// 错误口令由浏览器 SM4-GCM 标签校验发现。已取消登录密码恢复备份，
/// 仅保留独立加密口令和用户自行保存的离线恢复密钥。
/// </summary>
[Authorize]
public sealed class E2eeService(
    ISqlSugarRepository<UserE2eeIdentityEntity> repository,
    IUserE2eeArticleGuard articleGuard,
    ISqlSugarRepository<UserE2eePasskeyEntity> passkeys,
    ISqlSugarRepository<UserE2eePasskeyChallengeEntity> passkeyChallenges)
    : ApplicationService, IE2eeService
{

    /// <summary>
    /// 获取当前用户的 E2EE 身份配置；未启用时返回空。
    /// </summary>
    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "system-e2ee-identity")]
    public async Task<UserE2eeIdentityDto?> GetIdentityAsync()
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        var entity = await repository.GetAsync(
            identity => identity.UserId == userId && identity.IsActive);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// 为当前用户创建 E2EE 身份。
    /// </summary>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "system-e2ee-identity-write")]
    [SqlSugarUnitOfWork]
    public async Task<UserE2eeIdentityDto> PostIdentityAsync(CreateUserE2eeIdentityDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        ValidateCreate(input);

        var existing = await repository.GetAsync(identity => identity.UserId == userId);
        if (existing is not null)
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityExists,
                "E2EE identity already exists.");
        }


        var tenantId = TenantContext.CurrentTenantId;
        var entity = new UserE2eeIdentityEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantScope = UserE2eeIdentityEntity.ToTenantScope(tenantId),
            UserId = userId,
            KeyVersion = E2eeDefaults.InitialKeyVersion,
            KdfAlgorithm = input.KdfAlgorithm.Trim(),
            KdfSalt = input.KdfSalt.Trim(),
            KdfIterations = input.KdfIterations,
            KdfParameters = string.IsNullOrWhiteSpace(input.KdfParameters) ? null : input.KdfParameters,
            PasswordWrappedKey = input.PasswordWrappedKey.Trim(),
            RecoveryWrappedKey = input.RecoveryWrappedKey.Trim(),
            RecoveryKeyCreatedAt = DateTime.UtcNow,
            IsActive = true,
            Revision = 1
        };
        ClearAccountBackup(entity);

        await repository.InsertAsync(entity);
        Logger.LogInformation("E2EE identity created. UserId={UserId}", userId);
        return Map(entity);
    }

    /// <summary>
    /// 更新当前用户 E2EE 身份的加密口令封装。
    /// </summary>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "system-e2ee-identity-write")]
    [SqlSugarUnitOfWork]
    public async Task<UserE2eeIdentityDto> PutPassphraseAsync(UpdateUserE2eePassphraseDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        if (!E2eeWrapFormat.IsWrappedKey(input.PasswordWrappedKey))
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityInvalid,
                "Password wrapped key is not a valid wrap:v1 envelope.");
        }

        var entity = await GetActiveAsync(userId);
        EnsureRevision(entity, input.ExpectedRevision);
        entity.PasswordWrappedKey = input.PasswordWrappedKey.Trim();
        entity.Revision += 1;
        await repository.UpdateAsync(entity);
        Logger.LogInformation("E2EE passphrase wrap updated. UserId={UserId}", userId);
        return Map(entity);
    }

    /// <summary>
    /// 更新当前用户 E2EE 身份的离线恢复密钥封装。
    /// </summary>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "system-e2ee-identity-write")]
    [SqlSugarUnitOfWork]
    public async Task<UserE2eeIdentityDto> PutRecoveryKeyAsync(UpdateUserE2eeRecoveryKeyDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        if (!E2eeWrapFormat.IsWrappedKey(input.RecoveryWrappedKey))
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityInvalid,
                "Recovery wrapped key is not a valid wrap:v1 envelope.");
        }

        var entity = await GetActiveAsync(userId);
        EnsureRevision(entity, input.ExpectedRevision);
        entity.RecoveryWrappedKey = input.RecoveryWrappedKey.Trim();
        entity.RecoveryKeyCreatedAt = DateTime.UtcNow;
        entity.Revision += 1;
        await repository.UpdateAsync(entity);
        Logger.LogInformation("E2EE recovery key wrap updated. UserId={UserId}", userId);
        return Map(entity);
    }

    /// <summary>
    /// 重置当前用户的 E2EE 身份；存在已加密私有文章时禁止重置。
    /// </summary>
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "system-e2ee-identity-reset")]
    [SqlSugarUnitOfWork]
    public async Task<UserE2eeIdentityDto> ResetIdentityAsync(ResetUserE2eeIdentityDto input)
    {
        var userId = CurrentUser.Id ?? throw new AuthorizationException();
        ValidateCreate(input);

        // 重置会丢弃旧 UMK：只有确认没有任何已加密私有文章时才允许，否则旧密文将无法解密。
        var summary = await articleGuard.GetSummaryAsync(userId);
        if (summary.EncryptedPrivateArticleCount > 0)
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityResetBlocked,
                "E2EE identity cannot be reset while encrypted private articles exist.");
        }

        var entity = await GetActiveAsync(userId);
        EnsureRevision(entity, input.ExpectedRevision);
        var passkeyScope = UserE2eeIdentityEntity.ToTenantScope(TenantContext.CurrentTenantId);
        await passkeys.DeleteAsync(row => row.UserId == userId && row.TenantScope == passkeyScope);
        await passkeyChallenges.DeleteAsync(row => row.UserId == userId && row.TenantScope == passkeyScope);
        entity.KeyVersion += 1;
        entity.KdfAlgorithm = input.KdfAlgorithm.Trim();
        entity.KdfSalt = input.KdfSalt.Trim();
        entity.KdfIterations = input.KdfIterations;
        entity.KdfParameters = string.IsNullOrWhiteSpace(input.KdfParameters) ? null : input.KdfParameters;
        entity.PasswordWrappedKey = input.PasswordWrappedKey.Trim();
        entity.RecoveryWrappedKey = input.RecoveryWrappedKey.Trim();
        entity.RecoveryKeyCreatedAt = DateTime.UtcNow;
        ClearAccountBackup(entity);
        entity.Revision += 1;
        await repository.UpdateAsync(entity);
        Logger.LogInformation(
            "E2EE identity reset. UserId={UserId} KeyVersion={KeyVersion}",
            userId,
            entity.KeyVersion);
        return Map(entity);
    }

    private async Task<UserE2eeIdentityEntity> GetActiveAsync(Guid userId)
    {
        var entity = await repository.GetAsync(
            identity => identity.UserId == userId && identity.IsActive);
        return entity ?? throw new BusinessException(
            SystemErrorCodes.UserE2eeIdentityNotFound,
            "E2EE identity is not enabled for the current user.");
    }

    private static void EnsureRevision(UserE2eeIdentityEntity entity, int? expectedRevision)
    {
        if (expectedRevision.HasValue && expectedRevision.Value != entity.Revision)
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityConflict,
                "E2EE identity was updated on another device. Reload and try again.");
        }
    }

    private static void ValidateCreate(CreateUserE2eeIdentityDto input)
    {
        if (!string.Equals(input.KdfAlgorithm?.Trim(), E2eeDefaults.KdfAlgorithm, StringComparison.Ordinal))
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityInvalid,
                $"Unsupported KDF algorithm: {input.KdfAlgorithm}.");
        }

        if (!E2eeWrapFormat.IsValidSalt(input.KdfSalt))
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityInvalid,
                "KDF salt must be a 16-byte base64url value.");
        }

        if (!E2eeWrapFormat.IsWrappedKey(input.PasswordWrappedKey) ||
            !E2eeWrapFormat.IsWrappedKey(input.RecoveryWrappedKey))
        {
            throw new BusinessException(
                SystemErrorCodes.UserE2eeIdentityInvalid,
                "Wrapped keys must be wrap:v1 envelopes.");
        }

        ValidateAccountBackup(input);
    }

    private static void ValidateAccountBackup(CreateUserE2eeIdentityDto input)
    {
        if (input.AccountWrappedRecoveryKey is not null || input.AccountKdfSalt is not null ||
            input.AccountKdfIterations is not null || input.AccountPassword is not null)
            throw new BusinessException(SystemErrorCodes.UserE2eeIdentityInvalid,
                "Login-password recovery backups are no longer supported.");
    }

    private static void ClearAccountBackup(UserE2eeIdentityEntity entity)
    {
        entity.AccountWrappedRecoveryKey = null;
        entity.AccountKdfSalt = null;
        entity.AccountKdfIterations = null;
        entity.AccountRecoveryKeyUpdatedAt = null;
    }

    private static UserE2eeIdentityDto Map(UserE2eeIdentityEntity entity) => new()
    {
        UserId = entity.UserId,
        KeyVersion = entity.KeyVersion,
        Revision = entity.Revision,
        KdfAlgorithm = entity.KdfAlgorithm,
        KdfSalt = entity.KdfSalt,
        KdfIterations = entity.KdfIterations,
        KdfParameters = entity.KdfParameters,
        PasswordWrappedKey = entity.PasswordWrappedKey,
        RecoveryWrappedKey = entity.RecoveryWrappedKey,
        RecoveryKeyCreatedAt = entity.RecoveryKeyCreatedAt,
        AccountWrappedRecoveryKey = null,
        AccountKdfSalt = null,
        AccountKdfIterations = null,
        AccountRecoveryKeyUpdatedAt = null,
        LastModificationTime = entity.LastModificationTime
    };
}
