using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Domain.Security;

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// 密码最小长度
    /// </summary>
    public int PasswordMinLength { get; set; } = SecurityDefaults.PasswordMinLength;

    /// <summary>
    /// 密码是否需要包含大写字母
    /// </summary>
    public bool PasswordRequireUppercase { get; set; } = SecurityDefaults.PasswordRequireUppercase;

    /// <summary>
    /// 密码是否需要包含小写字母
    /// </summary>
    public bool PasswordRequireLowercase { get; set; } = SecurityDefaults.PasswordRequireLowercase;

    /// <summary>
    /// 密码是否需要包含数字
    /// </summary>
    public bool PasswordRequireDigit { get; set; } = SecurityDefaults.PasswordRequireDigit;

    /// <summary>
    /// 密码是否需要包含特殊字符
    /// </summary>
    public bool PasswordRequireSpecialChar { get; set; } = SecurityDefaults.PasswordRequireSpecialChar;

    /// <summary>
    /// 登录失败锁定阈值
    /// </summary>
    public int LoginFailureLockoutThreshold { get; set; } = SecurityDefaults.LoginMaxFailedAttempts;

    /// <summary>
    /// 锁定持续时间（分钟）
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = SecurityDefaults.LockoutDurationMinutes;

    /// <summary>
    /// 会话超时时间（分钟）
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// 各设备类型允许同时存在的登录会话数。0 表示禁止该类型登录。
    /// </summary>
    public DeviceSessionLimitOptions DeviceSessionLimits { get; set; } = new();

    /// <summary>
    /// Refresh Token 过期时间（天）
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Refresh Token 轮换间隔（分钟）。间隔内的刷新只签发新的 Access Token，
    /// 不更换 Refresh Token Cookie，避免请求被页面导航中止（499）后，
    /// 旧 Cookie 触发复用检测而吊销整个会话族。
    /// </summary>
    public int RefreshTokenRotationMinutes { get; set; } = 5;

    /// <summary>
    /// 密码哈希迭代次数
    /// </summary>
    public int PasswordHashIterations { get; set; } = 210000;

    /// <summary>
    /// HttpOnly Refresh Token Cookie 名称。
    /// </summary>
    public string RefreshTokenCookieName { get; set; } = "sim_refresh_token";

    /// <summary>
    /// 是否只允许通过 HTTPS 发送 Refresh Token Cookie。
    /// 生产环境必须为 true；本地 HTTP 代理开发可设为 false。
    /// </summary>
    public bool RefreshTokenCookieSecure { get; set; } = true;

    public int VerificationCodeExpirationMinutes { get; set; } =
        SecurityDefaults.VerificationCodeExpirationMinutes;
    public int VerificationCodeMaxAttempts { get; set; } =
        SecurityDefaults.VerificationCodeMaxAttempts;
    public int AuthenticatorSetupExpirationMinutes { get; set; } =
        SecurityDefaults.AuthenticatorSetupExpirationMinutes;
    public string AuthenticatorIssuer { get; set; } = BrandDefaults.Name;
    public EmailVerificationOptions EmailVerification { get; set; } = new();
    public SmsVerificationOptions SmsVerification { get; set; } = new();
}

public class DeviceSessionLimitOptions
{
    public int Pc { get; set; } = 1;
    public int Pad { get; set; } = 1;
    public int Phone { get; set; } = 1;
}

public class EmailVerificationOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = BrandDefaults.Name;
}

public class SmsVerificationOptions
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string AuthorizationHeader { get; set; } = string.Empty;
}
