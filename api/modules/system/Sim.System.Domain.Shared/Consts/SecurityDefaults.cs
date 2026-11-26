namespace Sim.System.Domain.Shared.Consts;

/// <summary>安全验证码与认证器绑定策略的系统默认值。</summary>
public static class SecurityDefaults
{
    public const int PasswordMinLength = 12;
    public const bool PasswordRequireUppercase = true;
    public const bool PasswordRequireLowercase = true;
    public const bool PasswordRequireDigit = true;
    public const bool PasswordRequireSpecialChar = true;

    public const int LoginMaxFailedAttempts = 5;
    public const int LockoutDurationMinutes = 30;

    public const int VerificationCodeExpirationMinutes = 5;
    public const int VerificationCodeMaxAttempts = 5;
    public const int AuthenticatorSetupExpirationMinutes = 10;
}
