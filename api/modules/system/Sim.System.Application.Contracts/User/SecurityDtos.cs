using System.ComponentModel.DataAnnotations;

namespace Sim.System.Application.Contracts.User;

public static class TwoFactorMethods
{
    public const string Authenticator = "authenticator";
    public const string Email = "email";
    public const string Sms = "sms";
    public const string RecoveryCode = "recovery";
}

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
    public required string ConfirmPassword { get; set; }

    /// <summary>
    /// 已废弃，仅保留旧客户端契约；服务端忽略这些备份字段，改密时清除存量备份。
    /// </summary>
    [StringLength(512)]
    public string? AccountWrappedRecoveryKey { get; set; }

    /// <summary>已废弃，服务端忽略。</summary>
    [StringLength(128)]
    public string? AccountKdfSalt { get; set; }

    [Range(10_000, 10_000_000)]
    public int? AccountKdfIterations { get; set; }
}

public class DeleteMyAccountRequest
{
    public required string Password { get; set; }
}

public class PasswordResetRequest
{
    public required string Account { get; set; }
    public required string Method { get; set; }
}

public class PasswordResetChallengeDto
{
    public Guid ChallengeId { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public class ResetPasswordRequest
{
    public Guid ChallengeId { get; set; }
    public required string Code { get; set; }
    public required string NewPassword { get; set; }
    public required string ConfirmPassword { get; set; }
}

public class TwoFactorCodeRequest
{
    public Guid ChallengeId { get; set; }
    public required string Method { get; set; }
}

public class CompleteTwoFactorLoginRequest : TwoFactorCodeRequest
{
    public required string Code { get; set; }
    public bool RememberMe { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string? DeviceName { get; set; }
}

public class LoginSessionDto
{
    public Guid Id { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsOnline { get; set; }
}

public class TwoFactorStatusDto
{
    public bool AuthenticatorEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public string? MaskedEmail { get; set; }
    public string? MaskedPhone { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public class BeginAuthenticatorSetupRequest
{
    public required string CurrentPassword { get; set; }
}

public class AuthenticatorSetupDto
{
    public required string SharedKey { get; set; }
    public required string AuthenticatorUri { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public class ConfirmAuthenticatorSetupRequest
{
    public required string Code { get; set; }
}

public class AuthenticatorRecoveryCodesDto
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];
}

public class MessageTwoFactorSetupRequest
{
    public required string Method { get; set; }
    public required string CurrentPassword { get; set; }
}

public class ConfirmMessageTwoFactorSetupRequest
{
    public Guid ChallengeId { get; set; }
    public required string Code { get; set; }
}

public class DisableTwoFactorRequest
{
    public required string Method { get; set; }
    public required string CurrentPassword { get; set; }
}
