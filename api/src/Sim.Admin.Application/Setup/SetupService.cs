using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.Admin.Application.Contracts.Setup;
using Sim.Admin.Domain.Data;
using Sim.Admin.Domain.Shared;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Auditing;
using Sim.Framework.Domain.Shared.Data;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Application.Contracts.User;
using Sim.System.Domain.Role;
using Sim.System.Domain.Security;
using Sim.System.Domain.SystemConfig;
using Sim.System.Domain.User;

namespace Sim.Admin.Application.Setup;

/// <summary>
/// 首次启动初始化：写入管理员账号、站点信息与安全策略，并标记站点已完成初始化。
/// 初始化接口只在站点尚未初始化时开放，完成后与普通业务接口一样受登录鉴权保护。
/// </summary>
[AllowAnonymous]
public sealed class SetupService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository,
    ISecurityManager securityManager,
    ISqlSugarRepository<SystemConfigurationItemEntity> configurationRepository,
    SetupStateReader setupStateReader,
    SystemUserDataSeeder systemUserDataSeeder,
    IEnumerable<IDataSeedContributor> seedContributors,
    IOptions<SecurityOptions> securityOptions,
    IConfiguration configuration,
    IEnumerable<IUserCreatedHandler> userCreatedHandlers) : AdminAppService, ISetupService
{
    private const string HostScope = "host";
    private static readonly SemaphoreSlim SetupGate = new(1, 1);
    private static readonly string[] AllowedThemes = ["auto", "light", "dark"];

    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "system-setup-status")]
    public async Task<SetupStatusDto> GetStatusAsync()
    {
        if (await setupStateReader.IsInitializedAsync())
        {
            return new SetupStatusDto { Initialized = true };
        }

        var settings = await LoadHostSettingsAsync();
        var security = securityOptions.Value;

        return new SetupStatusDto
        {
            Initialized = false,
            DefaultAdminUserName = DataSeedConsts.AdminUserName,
            SiteName = settings.GetValueOrDefault("site.name")
                ?? configuration["Branding:Name"]
                ?? "SimFramework",
            DefaultLanguage = settings.GetValueOrDefault("site.default-language") ?? "zh-Hans",
            DefaultTheme = settings.GetValueOrDefault("ui.default-theme") ?? "auto",
            RegistrationEnabled = bool.TryParse(
                settings.GetValueOrDefault("security.registration-enabled"),
                out var registrationEnabled) && registrationEnabled,
            LoginMaxFailedAttempts = ReadPositiveInt(
                settings.GetValueOrDefault("security.login-max-failed-attempts"),
                security.LoginFailureLockoutThreshold),
            LockoutDurationMinutes = ReadPositiveInt(
                settings.GetValueOrDefault("security.lockout-duration-minutes"),
                security.LockoutDurationMinutes)
        };
    }

    [RateLimit(permitLimit: 120, windowSeconds: 60, PartitionName = "system-setup-progress")]
    public Task<SetupProgressDto> GetProgressAsync()
    {
        var snapshot = SetupProgressTracker.Current;
        return Task.FromResult(new SetupProgressDto
        {
            Running = snapshot.Running,
            Stage = snapshot.Stage,
            Steps = BuildProgressStepKeys(),
            Percent = snapshot.Percent,
            Completed = snapshot.Completed,
            Error = snapshot.Error
        });
    }

    [HttpPost]
    [SqlSugarUnitOfWork]
    [RateLimit(permitLimit: 5, windowSeconds: 60, PartitionName = "system-setup")]
    public async Task InitializeAsync([FromBody] InitializeSystemDto input)
    {
        await SetupGate.WaitAsync();
        try
        {
            if (await setupStateReader.IsInitializedAsync())
            {
                throw new BusinessException(
                    AdminErrorCodes.SetupAlreadyCompleted,
                    "The system has already been initialized.");
            }

            if (input is null)
            {
                throw new BusinessException(
                    AdminErrorCodes.InvalidSetupData,
                    "The initialization settings are required.");
            }

            var userName = input.AdminUserName?.Trim() ?? string.Empty;
            var siteName = input.SiteName?.Trim() ?? string.Empty;
            var nickName = string.IsNullOrWhiteSpace(input.AdminNickName)
                ? "系统管理员"
                : input.AdminNickName.Trim();
            var language = NormalizeLanguage(input.DefaultLanguage);
            var theme = NormalizeTheme(input.DefaultTheme);

            if (userName.Length is < 3 or > 28)
            {
                throw new BusinessException(
                    AdminErrorCodes.InvalidSetupData,
                    "The administrator user name must contain 3 to 28 characters.");
            }

            if (nickName.Length > 20)
            {
                throw new BusinessException(
                    AdminErrorCodes.InvalidSetupData,
                    "The administrator display name cannot exceed 20 characters.");
            }

            if (siteName.Length == 0 || siteName.Length > 64)
            {
                throw new BusinessException(
                    AdminErrorCodes.InvalidSetupData,
                    "The site name must contain 1 to 64 characters.");
            }

            if (input.LoginMaxFailedAttempts is < 1 or > 20 ||
                input.LockoutDurationMinutes is < 1 or > 1440)
            {
                throw new BusinessException(
                    AdminErrorCodes.InvalidSetupData,
                    "The login lockout policy is out of range.");
            }

            var (isValid, errors) = securityManager.ValidatePassword(input.AdminPassword);
            if (!isValid)
            {
                throw new BusinessException(
                    errors.FirstOrDefault() ?? SystemErrorCodes.PasswordRequired,
                    "The password does not satisfy the password policy.");
            }

            var stepKeys = BuildProgressStepKeys();
            void ReportStep(string stage)
            {
                SetupProgressTracker.Report(stage, ResolveStepPercent(stepKeys, stage));
            }

            SetupProgressTracker.Begin();
            ReportStep(SetupProgressStages.Preparing);
            ReportStep(SetupProgressStages.Administrator);

            var admin = await ResolveAdministratorAsync(userName);
            var encryption = securityManager.EncryptPassword(input.AdminPassword);
            admin.NickName = nickName;
            admin.Name = userName;
            admin.IsActive = true;
            admin.IsBuiltIn = true;
            admin.AccessFailedCount = 0;
            admin.LockoutEnd = null;
            admin.EncryptedPassword = encryption.EncryptedPassword;
            admin.Salt = encryption.Salt;

            await userRepository.UpdateAsync(admin);

            // 初始化可能改写或新建管理员账号，按用户创建语义补齐各模块的关联数据。
            var adminContext = new UserCreatedContext(
                admin.Id,
                admin.UserName,
                admin.NickName,
                admin.TenantId);
            foreach (var handler in userCreatedHandlers)
            {
                await handler.HandleAsync(adminContext);
            }

            // 管理员账号创建后，由管理员作为创建人建立不可登录的系统账号。
            ReportStep(SetupProgressStages.SystemAccount);
            UserEntity systemUser;
            using (AuditContext.UseUser(admin.Id))
            {
                systemUser = await systemUserDataSeeder.EnsureAsync();
            }

            // 种子数据统一以系统账号作为创建人/修改人，每一步都会上报给进度接口。
            using (AuditContext.UseUser(systemUser.Id))
            {
                await EnsureBaseDataAsync(ReportStep);
            }
            ReportStep(SetupProgressStages.Finalizing);

            var role = await roleRepository.GetAsync(r => r.IsBuiltIn)
                ?? throw new BusinessException(
                    SystemErrorCodes.RoleNotFound,
                    "The administrator role is unavailable.");

            var existingLink = await userRoleRepository.GetAsync(link =>
                link.UserId == admin.Id && link.RoleId == role.Id);
            if (existingLink == null)
            {
                await userRoleRepository.InsertAsync(new UserRole
                {
                    UserId = admin.Id,
                    RoleId = role.Id
                });
            }

            await UpsertSettingAsync("system.site", "站点信息", "site.name", "站点名称",
                siteName, "站点展示名称", 10);
            await UpsertSettingAsync("system.site", "站点信息", "site.default-language", "默认语言",
                language, "支持 zh-Hans 和 en", 20);
            await UpsertSettingAsync("system.security", "安全策略", "security.registration-enabled", "开放注册",
                input.RegistrationEnabled ? "true" : "false", "控制自助注册入口", 10);
            await UpsertSettingAsync("system.security", "安全策略", "security.login-max-failed-attempts", "登录失败上限",
                input.LoginMaxFailedAttempts.ToString(CultureInfo.InvariantCulture), "触发锁定的连续失败次数", 30);
            await UpsertSettingAsync("system.security", "安全策略", "security.lockout-duration-minutes", "锁定时长",
                input.LockoutDurationMinutes.ToString(CultureInfo.InvariantCulture), "登录锁定的持续分钟数", 40);
            await UpsertSettingAsync("system.ui", "分页与界面", "ui.default-theme", "默认主题",
                theme, "auto、light 或 dark", 30);
            await UpsertSettingAsync("system.setup", "系统初始化", DataSeedConsts.SetupStateConfigKey, "初始化状态",
                DataSeedConsts.SetupStateCompleted, "completed 表示站点已完成首次初始化", 10);

            SetupProgressTracker.Complete();

            Logger.LogInformation(
                "系统初始化完成: 管理员 {UserName}, 站点 {SiteName}, 语言 {Language}, 主题 {Theme}",
                userName,
                siteName,
                language,
                theme);
        }
        catch (Exception ex)
        {
            SetupProgressTracker.Fail(ex.Message);
            throw;
        }
        finally
        {
            SetupGate.Release();
        }
    }

    private async Task<UserEntity> ResolveAdministratorAsync(string userName)
    {
        var requested = await userRepository.GetAsync(user => user.UserName == userName);
        var defaultAdmin = await userRepository.GetAsync(user =>
            user.UserName == DataSeedConsts.AdminUserName);

        if (requested != null)
        {
            if (defaultAdmin?.Id != requested.Id)
            {
                throw new BusinessException(
                    SystemErrorCodes.UserNameAlreadyExists,
                    "The administrator user name is already in use.");
            }

            return requested;
        }

        if (defaultAdmin != null)
        {
            // 默认账号尚未被使用：初始化时直接把它改成用户选择的账号。
            defaultAdmin.UserName = userName;
            return defaultAdmin;
        }

        var admin = new UserEntity
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = null,
            Name = userName,
            IsActive = true
        };
        await userRepository.InsertAsync(admin);
        return admin;
    }

    private async Task EnsureBaseDataAsync(Action<string>? onStep = null)
    {
        // 种子幂等；无论此前是否执行过，都在初始化时再补齐一次，避免部分失败后留下缺模块/权限的站点。
        Logger.LogInformation("执行基础数据种子，确保初始化所需的角色与权限已就绪...");
        var contributors = seedContributors as IReadOnlyList<IDataSeedContributor>
            ?? seedContributors.ToList();
        foreach (var contributor in contributors)
        {
            await contributor.SeedAsync(step => onStep?.Invoke(step.Key));
        }

        var role = await roleRepository.GetAsync(r => r.IsBuiltIn);
        if (role == null)
        {
            throw new BusinessException(
                SystemErrorCodes.RoleNotFound,
                "The administrator role is unavailable.");
        }
    }

    /// <summary>
    /// 初始化步骤顺序：准备、管理员、系统账号、种子步骤、收尾。
    /// </summary>
    private string[] BuildProgressStepKeys()
    {
        var contributors = seedContributors as IReadOnlyList<IDataSeedContributor>
            ?? seedContributors.ToList();
        var keys = new List<string>
        {
            SetupProgressStages.Preparing,
            SetupProgressStages.Administrator,
            SetupProgressStages.SystemAccount
        };
        keys.AddRange(contributors
            .SelectMany(contributor => contributor.Steps)
            .Select(step => step.Key));
        keys.Add(SetupProgressStages.Finalizing);
        return keys.ToArray();
    }

    private static int ResolveStepPercent(string[] stepKeys, string stage)
    {
        if (stepKeys.Length == 0)
        {
            return 0;
        }

        var index = Array.IndexOf(stepKeys, stage);
        if (index < 0)
        {
            return 0;
        }

        return (int)Math.Round((index + 0.5d) * 100d / stepKeys.Length);
    }

    private async Task<Dictionary<string, string>> LoadHostSettingsAsync()
    {
        var items = await configurationRepository.GetListAsync(item =>
            item.TenantScope == HostScope && item.Enabled);
        return items
            .GroupBy(item => item.ConfigKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task UpsertSettingAsync(
        string groupKey,
        string groupName,
        string configKey,
        string configName,
        string value,
        string description,
        int sort)
    {
        var item = await configurationRepository.GetAsync(entity =>
            entity.TenantScope == HostScope &&
            entity.GroupKey == groupKey &&
            entity.ConfigKey == configKey);

        if (item == null)
        {
            await configurationRepository.InsertAsync(new SystemConfigurationItemEntity(Guid.NewGuid())
            {
                TenantScope = HostScope,
                GroupKey = groupKey,
                GroupName = groupName,
                ConfigKey = configKey,
                ConfigName = configName,
                Value = value,
                Description = description,
                Sort = sort,
                Enabled = true
            });
            return;
        }

        item.Value = value;
        item.Enabled = true;
        await configurationRepository.UpdateAsync(item);
    }

    private static string NormalizeLanguage(string? language) =>
        (language ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "zh-hans" or "zh-cn" or "zh" => "zh-Hans",
            "en" or "en-us" or "en-gb" => "en",
            _ => throw new BusinessException(
                AdminErrorCodes.InvalidSetupData,
                "The selected default language is not supported.")
        };

    private static string NormalizeTheme(string? theme)
    {
        var normalized = (theme ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedThemes.Contains(normalized)
            ? normalized
            : throw new BusinessException(
                AdminErrorCodes.InvalidSetupData,
                "The selected default theme is not supported.");
    }

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
