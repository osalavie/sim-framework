using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
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
        new("system.site", "站点信息", "site.name", "站点名称", "SimFramework", "默认站点展示名称", 10),
        new("system.site", "站点信息", "site.default-language", "默认语言", "zh-Hans", "支持 zh-Hans 和 en", 20),
        new("system.site", "站点信息", "site.timezone", "默认时区", "Asia/Shanghai", "用于报表和时间展示", 30),

        new("system.security", "安全策略", "security.registration-enabled", "开放注册", "false", "控制自助注册入口", 10),
        new("system.security", "安全策略", "security.registration-role-codes", "注册默认角色", "User", "自助注册成功后自动分配的角色代码，多个用英文逗号分隔；仅非内置角色生效，留空时回退 User", 15),
        new("system.security", "安全策略", "security.password-min-length", "密码最小长度", "12", "新密码最小字符数", 20),
        new("system.security", "安全策略", "security.login-max-failed-attempts", "登录失败上限", "5", "触发锁定的连续失败次数", 30),
        new("system.security", "安全策略", "security.lockout-duration-minutes", "锁定时长", "30", "登录锁定的持续分钟数", 40),
        new("system.security", "安全策略", "security.user-default-password", "用户默认密码", GeneratedUserDefaultPassword, "新建用户与重置密码时使用的初始密码；种子生成随机值，可在此修改，需满足密码策略", 50),

        new("system.file", "文件能力", "file.upload-max-mb", "上传上限", "50", "单个文件上传大小上限（MB）", 10),
        new("system.file", "文件能力", "file.avatar-max-kb", "头像上限", "512", "头像压缩上传上限（KB）", 20),
        new("system.file", "文件能力", "file.antivirus-enabled", "病毒扫描", "false", "是否启用 ClamAV 扫描", 30),

        new("system.ui", "分页与界面", "ui.default-page-size", "默认分页", "20", "后台列表默认每页条数", 10),
        new("system.ui", "分页与界面", "ui.max-page-size", "最大分页", "100", "后台列表允许的最大每页条数", 20),
        new("system.ui", "分页与界面", "ui.default-theme", "默认主题", "auto", "auto、light 或 dark", 30)
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
