using System;
using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.E2ee;

/// <summary>
/// 当前登录用户的 E2EE 身份。只包含 KDF 参数与包裹后的 UMK；
/// 服务端从不接收或返回明文 UMK、恢复密钥或加密口令。
/// </summary>
public sealed class UserE2eeIdentityDto
{
    public Guid UserId { get; set; }

    public int KeyVersion { get; set; }

    public int Revision { get; set; }

    /// <summary>v1 固定 PBKDF2-SM3。</summary>
    public string KdfAlgorithm { get; set; } = string.Empty;

    /// <summary>16B 随机 salt 的 base64url。</summary>
    public string KdfSalt { get; set; } = string.Empty;

    public int KdfIterations { get; set; }

    public string? KdfParameters { get; set; }

    /// <summary>wrap:v1:... 被加密口令 KEK 包裹的 UMK。</summary>
    public string PasswordWrappedKey { get; set; } = string.Empty;

    /// <summary>wrap:v1:... 被恢复密钥包裹的 UMK。</summary>
    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public DateTime? RecoveryKeyCreatedAt { get; set; }

    /// <summary>已废弃：响应始终为空，旧登录密码恢复备份由升级迁移清除。</summary>
    public string? AccountWrappedRecoveryKey { get; set; }

    /// <summary>登录密码 KEK 的 16B salt 的 base64url；未启用账号密码备份时为空。</summary>
    public string? AccountKdfSalt { get; set; }

    public int? AccountKdfIterations { get; set; }

    public DateTime? AccountRecoveryKeyUpdatedAt { get; set; }

    public DateTime? LastModificationTime { get; set; }
}

/// <summary>首次启用 E2EE；服务端只落库包裹结果与 KDF 参数。</summary>
public class CreateUserE2eeIdentityDto
{
    [Required]
    [StringLength(32)]
    public string KdfAlgorithm { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string KdfSalt { get; set; } = string.Empty;

    [Range(10_000, 10_000_000)]
    public int KdfIterations { get; set; }

    [StringLength(2048)]
    public string? KdfParameters { get; set; }

    [Required]
    [StringLength(512)]
    public string PasswordWrappedKey { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string RecoveryWrappedKey { get; set; } = string.Empty;

    /// <summary>已废弃：仅保留契约兼容性，提交账号密码备份字段会被拒绝。</summary>
    [StringLength(512)]
    public string? AccountWrappedRecoveryKey { get; set; }

    /// <summary>可选账号密码备份的 16B salt 的 base64url。</summary>
    [StringLength(128)]
    public string? AccountKdfSalt { get; set; }

    [Range(10_000, 10_000_000)]
    public int? AccountKdfIterations { get; set; }

    /// <summary>
    /// 已废弃：不得提交登录密码，非空会被拒绝。
    /// </summary>
    [StringLength(256)]
    public string? AccountPassword { get; set; }
}

/// <summary>
/// 重置 E2EE 身份：仅在没有已加密私有文章时允许，客户端生成全新的 UMK/RK 与口令包裹。
/// </summary>
public sealed class ResetUserE2eeIdentityDto : CreateUserE2eeIdentityDto
{
    /// <summary>客户端最近一次读取到的身份版本；不匹配时返回冲突，避免多设备覆盖。</summary>
    public int? ExpectedRevision { get; set; }
}

/// <summary>修改加密口令：客户端先用旧口令解开 UMK，再提交新的 PasswordWrap。</summary>
public sealed class UpdateUserE2eePassphraseDto
{
    [Required]
    [StringLength(512)]
    public string PasswordWrappedKey { get; set; } = string.Empty;

    /// <summary>客户端最近一次读取到的身份版本；不匹配时返回冲突，避免多设备覆盖。</summary>
    public int? ExpectedRevision { get; set; }
}

/// <summary>轮换恢复密钥：客户端生成新 RK 后提交新的 RecoveryWrap。</summary>
public sealed class UpdateUserE2eeRecoveryKeyDto
{
    [Required]
    [StringLength(512)]
    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public int? ExpectedRevision { get; set; }
}

/// <summary>
/// 当前登录用户的 E2EE 身份接口。仅本人可读写，接口只返回包裹数据。
/// </summary>
public interface IE2eeService : IApplicationService
{
    Task<UserE2eeIdentityDto?> GetIdentityAsync();

    Task<UserE2eeIdentityDto> PostIdentityAsync(CreateUserE2eeIdentityDto input);

    Task<UserE2eeIdentityDto> PutPassphraseAsync(UpdateUserE2eePassphraseDto input);

    Task<UserE2eeIdentityDto> PutRecoveryKeyAsync(UpdateUserE2eeRecoveryKeyDto input);

    /// <summary>重置身份（UMK/RK/口令全部重新生成）；由内容模块的资源守卫校验没有已加密私有资源。</summary>
    Task<UserE2eeIdentityDto> ResetIdentityAsync(ResetUserE2eeIdentityDto input);
}

/// <summary>当前用户的私有资源计数；用于判断是否可以安全重置 E2EE 身份。</summary>
public sealed class UserE2eeArticleSummary
{
    public int PrivateArticleCount { get; set; }

    public int EncryptedPrivateArticleCount { get; set; }
}

/// <summary>
/// 由内容模块实现：按用户统计私有资源与已加密私有资源数量。
/// System 模块不直接依赖内容模块，以便身份重置仍能由服务端强制校验。
/// </summary>
public interface IUserE2eeArticleGuard
{
    Task<UserE2eeArticleSummary> GetSummaryAsync(Guid userId);
}
