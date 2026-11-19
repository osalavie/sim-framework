using System.Windows;

namespace Sim.Admin.Wpf.Client.Controls;

/// <summary>Naive UI 的 small / medium / large 三档尺寸，WPF 统一映射为 30 / 36 / 42 像素。</summary>
public enum CustomControlSize
{
    Small,
    Medium,
    Large
}

/// <summary>通用语义色，对应 Naive UI 的 success / warning / error / info 状态。</summary>
public enum CustomTone
{
    Default,
    Primary,
    Info,
    Success,
    Warning,
    Error
}

/// <summary>文本语义色，用于 CustomText 与辅助说明。</summary>
public enum CustomTextTone
{
    Default,
    Muted,
    Primary,
    Success,
    Warning,
    Error
}

/// <summary>WPF 组件的尺寸与圆角基线，供各控件样式和代码后置共用。</summary>
public static class CustomMetrics
{
    public const double SmallHeight = 30;
    public const double MediumHeight = 36;
    public const double LargeHeight = 42;

    public const double SmallRadius = 8;
    public const double MediumRadius = 10;
    public const double LargeRadius = 12;

    public static double Height(CustomControlSize size) => size switch
    {
        CustomControlSize.Small => SmallHeight,
        CustomControlSize.Large => LargeHeight,
        _ => MediumHeight
    };

    public static double Radius(CustomControlSize size) => size switch
    {
        CustomControlSize.Small => SmallRadius,
        CustomControlSize.Large => LargeRadius,
        _ => MediumRadius
    };

    public static double FontSize(CustomControlSize size) => size switch
    {
        CustomControlSize.Small => 12,
        CustomControlSize.Large => 14,
        _ => 13
    };

    public static Thickness Padding(CustomControlSize size) => size switch
    {
        CustomControlSize.Small => new Thickness(10, 3, 10, 3),
        CustomControlSize.Large => new Thickness(16, 8, 16, 8),
        _ => new Thickness(12, 5, 12, 5)
    };

    public static CornerRadius Corner(CustomControlSize size) => new(Radius(size));
}
