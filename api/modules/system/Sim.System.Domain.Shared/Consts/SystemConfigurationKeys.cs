namespace Sim.System.Domain.Shared.Consts;

/// <summary>系统设置分组键。</summary>
public static class SystemConfigurationGroups
{
    public const string Site = "system.site";
    public const string Security = "system.security";
    public const string File = "system.file";
    public const string Ui = "system.ui";
    public const string Setup = "system.setup";
}

/// <summary>系统设置配置键。</summary>
public static class SystemConfigurationKeys
{
    public const string SiteName = "site.name";
    public const string SiteDefaultLanguage = "site.default-language";
    public const string SiteTimezone = "site.timezone";

    public const string RegistrationEnabled = "security.registration-enabled";
    public const string RegistrationRoleCodes = "security.registration-role-codes";
    public const string PasswordMinLength = "security.password-min-length";
    public const string PasswordRequireUppercase = "security.password-require-uppercase";
    public const string PasswordRequireLowercase = "security.password-require-lowercase";
    public const string PasswordRequireDigit = "security.password-require-digit";
    public const string PasswordRequireSpecialChar = "security.password-require-special-char";
    public const string LoginMaxFailedAttempts = "security.login-max-failed-attempts";
    public const string LockoutDurationMinutes = "security.lockout-duration-minutes";
    public const string UserDefaultPassword = "security.user-default-password";
    public const string AuthenticatorIssuer = "security.authenticator-issuer";
    public const string VerificationCodeExpirationMinutes =
        "security.verification-code-expiration-minutes";
    public const string VerificationCodeMaxAttempts = "security.verification-code-max-attempts";
    public const string AuthenticatorSetupExpirationMinutes =
        "security.authenticator-setup-expiration-minutes";

    public const string FileUploadMaxMb = "file.upload-max-mb";
    public const string FileAvatarMaxKb = "file.avatar-max-kb";
    public const string FileAntivirusEnabled = "file.antivirus-enabled";

    public const string UiDefaultPageSize = "ui.default-page-size";
    public const string UiMaxPageSize = "ui.max-page-size";
    public const string UiDefaultTheme = "ui.default-theme";
}
