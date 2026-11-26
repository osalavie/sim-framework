using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.SystemConfig;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// Idempotently seeds host-scoped system configuration entries.
/// Existing rows are preserved so administrators may safely adjust runtime values.
/// </summary>
public class SystemConfigurationDataSeeder
{
    private sealed record ConfigurationSeed(
        string GroupKey,
        string GroupName,
        string ConfigKey,
        string ConfigName,
        string Value,
        string? Description,
        int Sort,
        bool Enabled = true);

    private static readonly string GeneratedUserDefaultPassword = GenerateRandomPassword();

    private static readonly ConfigurationSeed[] Seeds =
    [
        new(SystemConfigurationGroups.Site, "站点信息",
            SystemConfigurationKeys.SiteName, "站点名称", BrandDefaults.Name, "默认站点展示名称", 10),
        new(SystemConfigurationGroups.Site, "站点信息",
            SystemConfigurationKeys.SiteDefaultLanguage, "默认语言", "zh-Hans", "支持 zh-Hans 和 en", 20),
        new(SystemConfigurationGroups.Site, "站点信息",
            SystemConfigurationKeys.SiteTimezone, "默认时区", "Asia/Shanghai", "用于报表和时间展示", 30),

        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.RegistrationEnabled, "开放注册", "false", "控制自助注册入口", 10),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.RegistrationRoleCodes, "注册默认角色", "User",
            "自助注册成功后自动分配的角色代码，多个用英文逗号分隔；仅非内置角色生效，留空时回退 User", 15),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.PasswordMinLength, "密码最小长度",
            SecurityDefaults.PasswordMinLength.ToString(CultureInfo.InvariantCulture),
            "新密码最小字符数", 20),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.PasswordRequireUppercase, "密码需包含大写字母",
            "true", "是否要求密码包含大写字母", 21),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.PasswordRequireLowercase, "密码需包含小写字母",
            "true", "是否要求密码包含小写字母", 22),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.PasswordRequireDigit, "密码需包含数字",
            "true", "是否要求密码包含数字", 23),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.PasswordRequireSpecialChar, "密码需包含特殊字符",
            "true", "是否要求密码包含特殊字符", 24),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.AuthenticatorIssuer, "认证器显示名", BrandDefaults.Name,
            "TOTP 认证器中显示的发行方名称", 25),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.VerificationCodeExpirationMinutes, "验证码有效期（分钟）",
            SecurityDefaults.VerificationCodeExpirationMinutes.ToString(CultureInfo.InvariantCulture),
            "登录、注册、找回密码与两步验证验证码的有效分钟数", 26),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.VerificationCodeMaxAttempts, "验证码最大尝试次数",
            SecurityDefaults.VerificationCodeMaxAttempts.ToString(CultureInfo.InvariantCulture),
            "验证码连续输错上限，达到后挑战失效", 27),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.AuthenticatorSetupExpirationMinutes, "认证器绑定有效期（分钟）",
            SecurityDefaults.AuthenticatorSetupExpirationMinutes.ToString(CultureInfo.InvariantCulture),
            "TOTP 认证器绑定挑战的有效分钟数", 28),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.LoginMaxFailedAttempts, "登录失败上限",
            SecurityDefaults.LoginMaxFailedAttempts.ToString(CultureInfo.InvariantCulture),
            "触发锁定的连续失败次数", 30),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.LockoutDurationMinutes, "锁定时长",
            SecurityDefaults.LockoutDurationMinutes.ToString(CultureInfo.InvariantCulture),
            "登录锁定的持续分钟数", 40),
        new(SystemConfigurationGroups.Security, "安全策略",
            SystemConfigurationKeys.UserDefaultPassword, "用户默认密码", GeneratedUserDefaultPassword,
            "新建用户与重置密码时使用的初始密码；种子生成随机值，可在此修改，需满足密码策略", 50),

        new(SystemConfigurationGroups.File, "文件能力",
            SystemConfigurationKeys.FileUploadMaxMb, "上传上限", "50", "单个文件上传大小上限（MB）", 10),
        new(SystemConfigurationGroups.File, "文件能力",
            SystemConfigurationKeys.FileAvatarMaxKb, "头像上限", "512", "头像压缩上传上限（KB）", 20),
        new(SystemConfigurationGroups.File, "文件能力",
            SystemConfigurationKeys.FileAntivirusEnabled, "病毒扫描", "false", "是否启用 ClamAV 扫描", 30),

        new(SystemConfigurationGroups.Ui, "分页与界面",
            SystemConfigurationKeys.UiDefaultPageSize, "默认分页", "20", "后台列表默认每页条数", 10),
        new(SystemConfigurationGroups.Ui, "分页与界面",
            SystemConfigurationKeys.UiMaxPageSize, "最大分页", "100", "后台列表允许的最大每页条数", 20),
        new(SystemConfigurationGroups.Ui, "分页与界面",
            SystemConfigurationKeys.UiDefaultTheme, "默认主题", "auto", "auto、light 或 dark", 30)
    ];

    private readonly ISqlSugarRepository<SystemConfigurationItemEntity> _repository;
    private readonly ILogger<SystemConfigurationDataSeeder> _logger;

    public SystemConfigurationDataSeeder(
        ISqlSugarRepository<SystemConfigurationItemEntity> repository,
        ILogger<SystemConfigurationDataSeeder> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var created = 0;
        foreach (var seed in Seeds)
        {
            var exists = await _repository.GetAsync(item =>
                item.TenantScope == "host" &&
                item.GroupKey == seed.GroupKey &&
                item.ConfigKey == seed.ConfigKey);
            if (exists != null)
            {
                continue;
            }

            await _repository.InsertAsync(new SystemConfigurationItemEntity(Guid.NewGuid())
            {
                TenantScope = "host",
                GroupKey = seed.GroupKey,
                GroupName = seed.GroupName,
                ConfigKey = seed.ConfigKey,
                ConfigName = seed.ConfigName,
                Value = seed.Value,
                Description = seed.Description,
                Sort = seed.Sort,
                Enabled = seed.Enabled
            });
            created++;
        }

        _logger.LogInformation("系统配置种子检查完成，新增 {Created} 项", created);
    }

    /// <summary>
    /// 生成满足默认密码策略的随机初始密码，避免源码中出现可用的固定默认密码。
    /// </summary>
    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";

        return string.Concat(
            RandomNumberGenerator.GetString(upper, 3),
            RandomNumberGenerator.GetString(lower, 4),
            RandomNumberGenerator.GetString(digits, 3),
            "!a");
    }
}
