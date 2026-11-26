using System.ComponentModel.DataAnnotations;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Contracts.User;

/// <summary>当前登录用户已保存的偏好。</summary>
public class UserPreferenceDto
{
    public string Language { get; set; } = UserPreferenceDefaults.Language;

    /// <summary>light / dark / system</summary>
    public string ThemeMode { get; set; } = UserPreferenceDefaults.ThemeMode;

    /// <summary>主题风格标识，由前端主题注册表解释。</summary>
    public string ThemeStyle { get; set; } = UserPreferenceDefaults.ThemeStyle;
}

/// <summary>更新当前登录用户的偏好；字段为空时保持原值。</summary>
public class UpdateUserPreferenceDto
{
    [StringLength(UserPreferenceDefaults.MaxLanguageLength)]
    public string? Language { get; set; }

    [StringLength(UserPreferenceDefaults.MaxThemeModeLength)]
    public string? ThemeMode { get; set; }

    [StringLength(UserPreferenceDefaults.MaxThemeStyleLength)]
    public string? ThemeStyle { get; set; }
}
