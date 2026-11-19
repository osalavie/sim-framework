using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Admin.Application.Contracts.Setup;

/// <summary>
/// 系统初始化状态与首次启动默认值。
/// </summary>
public sealed class SetupStatusDto
{
    /// <summary>
    /// 是否已完成系统初始化。
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>
    /// 默认管理员账号（仅未初始化时返回，用于预填）。
    /// </summary>
    public string? DefaultAdminUserName { get; set; }

    /// <summary>
    /// 默认站点名称（仅未初始化时返回）。
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    /// 默认站点语言（仅未初始化时返回）。
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// 是否默认开放注册（仅未初始化时返回）。
    /// </summary>
    public bool RegistrationEnabled { get; set; }

    /// <summary>
    /// 默认登录失败锁定阈值（仅未初始化时返回）。
    /// </summary>
    public int LoginMaxFailedAttempts { get; set; }

    /// <summary>
    /// 默认锁定时长（分钟，仅未初始化时返回）。
    /// </summary>
    public int LockoutDurationMinutes { get; set; }

    /// <summary>
    /// 默认主题：auto、light 或 dark（仅未初始化时返回）。
    /// </summary>
    public string? DefaultTheme { get; set; }
}

/// <summary>
/// 首次初始化进度。
/// </summary>
public sealed class SetupProgressDto
{
    /// <summary>
    /// 是否正在执行初始化。
    /// </summary>
    public bool Running { get; set; }

    /// <summary>
    /// 当前阶段：idle、preparing、administrator、system-account、种子步骤、finalizing、completed、failed。
    /// </summary>
    public string Stage { get; set; } = "idle";

    /// <summary>
    /// 初始化步骤顺序。前端按此列表逐项展示进度。
    /// </summary>
    public IReadOnlyList<string> Steps { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 当前完成百分比（0-100）。
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// 是否已完成。
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// 失败原因，仅失败时返回。
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// 首次启动初始化请求。
/// </summary>
public sealed class InitializeSystemDto
{
    [Required]
    [StringLength(28, MinimumLength = 3)]
    public string AdminUserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string AdminPassword { get; set; } = string.Empty;

    [StringLength(20)]
    public string? AdminNickName { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string SiteName { get; set; } = string.Empty;

    [Required]
    [StringLength(16)]
    public string DefaultLanguage { get; set; } = string.Empty;

    [Required]
    [StringLength(8)]
    public string DefaultTheme { get; set; } = string.Empty;

    public bool RegistrationEnabled { get; set; }

    [Range(1, 20)]
    public int LoginMaxFailedAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutDurationMinutes { get; set; } = 30;
}

/// <summary>
/// 系统初始化应用服务。
/// </summary>
public interface ISetupService : IApplicationService
{
    /// <summary>
    /// 获取初始化状态和首次启动默认值。
    /// </summary>
    Task<SetupStatusDto> GetStatusAsync();

    /// <summary>
    /// 获取首次初始化进度。
    /// </summary>
    Task<SetupProgressDto> GetProgressAsync();

    /// <summary>
    /// 执行系统初始化：创建/更新默认管理员账号并写入站点与安全策略。
    /// </summary>
    Task InitializeAsync(InitializeSystemDto input);
}
