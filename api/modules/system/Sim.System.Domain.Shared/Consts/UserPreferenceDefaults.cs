namespace Sim.System.Domain.Shared.Consts;

/// <summary>
/// 用户前端偏好的默认值与允许值。语言、明暗模式由后端约束，
/// 主题风格只做长度与字符白名单校验，新增风格无需再次改后端。
/// </summary>
public static class UserPreferenceDefaults
{
    public const string LanguageZhCn = "zh-CN";
    public const string LanguageEnUs = "en-US";
    public const string Language = LanguageZhCn;

    public const string ThemeModeLight = "light";
    public const string ThemeModeDark = "dark";
    public const string ThemeModeSystem = "system";
    public const string ThemeMode = ThemeModeSystem;

    public const string ThemeStyle = "paper-ocean";

    public const int MaxLanguageLength = 16;
    public const int MaxThemeModeLength = 16;
    public const int MaxThemeStyleLength = 32;

    public static readonly IReadOnlySet<string> SupportedLanguages =
        new HashSet<string>(StringComparer.Ordinal) { LanguageZhCn, LanguageEnUs };

    public static readonly IReadOnlySet<string> SupportedThemeModes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ThemeModeLight,
            ThemeModeDark,
            ThemeModeSystem
        };
}
