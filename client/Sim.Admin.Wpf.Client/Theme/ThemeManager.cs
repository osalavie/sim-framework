using System.Windows;
using System.Windows.Media;

namespace Sim.Admin.Wpf.Client.Theme;

// Values mirror the eight palettes in web/src/config/themes.ts.
// WPF 组件只使用 2px 描边表达层级，不使用任何投影（含硬投影和模糊阴影）。
public static class ThemeManager
{
    public static readonly string[] Styles = ["纸上海洋", "玄青·鎏金", "竹月·清雅", "朱缃·典章"];
    public static readonly string[] Ids = ["paper-ocean", "xuanqing", "zhuyue", "zhuxiang"];

    private sealed record Palette(
        string Background, string BgDeep, string Paper, string Ink, string Muted,
        string Line, string Edge, string Primary, string OnPrimary, string Action, string OnAction, string Soft,
        string Success, string SuccessSoft, string Warning, string WarningSoft,
        string Info, string InfoSoft, string Hover, string Overlay, string DangerSoft);

    private static readonly Palette[,] Palettes =
    {
        {
            new("#FFF9EE", "#F9F2E3", "#FFFFFF", "#14284B", "#4F6282", "#D9E2F1", "#14284B", "#2F6BFF", "#FFFFFF",
                "#FF7A6B", "#14284B", "#E7F1FF",
                "#2E7D4F", "#E6F5EC", "#A9761A", "#FBF2DD", "#2F6BFF", "#E7F1FF", "#EEF3FB", "#6614284B", "#FBE7E4"),
            new("#121A2B", "#182236", "#1C2740", "#F4EEE1", "#AAB5CB", "#33415F", "#F4EEE1", "#7EA2FF", "#14284B",
                "#FF8B79", "#14284B", "#22304D",
                "#6FCB94", "#1F3A2C", "#E0B458", "#3B2F1B", "#7EA2FF", "#22304D", "#263453", "#B3000409", "#3A1F22")
        },
        {
            new("#F5F6F4", "#ECEFF0", "#FFFFFF", "#1B2A33", "#52636D", "#DDE4E1", "#CFD8D5", "#2E5065", "#F7F6F1",
                "#2E5065", "#F7F6F1", "#E8EFF3",
                "#2E6B52", "#E7F1EA", "#9A761B", "#F6EFDC", "#33596F", "#E8EFF3", "#EEF2F1", "#661B2A33", "#F6E5E5"),
            new("#0F161B", "#141E24", "#19242B", "#EDF0EC", "#A7B4BA", "#2A3A42", "#36464E", "#8FB6CF", "#0F161B",
                "#8FB6CF", "#0F161B", "#23333C",
                "#86C9A1", "#1B3226", "#DDB868", "#352C1B", "#8FB6CF", "#23333C", "#22303A", "#B3000406", "#382124")
        },
        {
            new("#F3F6F3", "#E9EFEB", "#FFFFFF", "#1F2C27", "#52645C", "#DCE5DF", "#CCD9D2", "#3E6B57", "#F7F8F4",
                "#3E6B57", "#F7F8F4", "#E6F0EA",
                "#2F6E4F", "#E8F3EC", "#9C7A1F", "#F6F0DC", "#3E6B57", "#E6F0EA", "#EDF3EF", "#661F2C27", "#F7E6E4"),
            new("#0E1512", "#131C18", "#19231F", "#E9EFEA", "#A6B4AC", "#293A33", "#35463E", "#8FC0A6", "#0E1512",
                "#8FC0A6", "#0E1512", "#23342C",
                "#8FC0A6", "#1F3428", "#D9B26A", "#352D1C", "#8FC0A6", "#23342C", "#21342B", "#B3000403", "#38241F")
        },
        {
            new("#F8F4EA", "#F1EADB", "#FFFDF8", "#2B2420", "#675B52", "#E2D8C6", "#D8CAB2", "#A93B2E", "#FFF8EF",
                "#A93B2E", "#FFF8EF", "#F7E7E2",
                "#4C7A50", "#EAF2E8", "#A66A1E", "#F8EEDD", "#3E6C9E", "#E9F0F7", "#F5EFE4", "#662B2420", "#F8E8E3"),
            new("#151110", "#1B1614", "#221C19", "#F2EBDF", "#B8ABA0", "#322924", "#433830", "#E89A8D", "#151110",
                "#E89A8D", "#151110", "#3A2521",
                "#8DBE8E", "#26301F", "#D9A97A", "#33291F", "#9DBBD8", "#26313B", "#2E2723", "#B3020000", "#3A221E")
        }
    };

    public static void Apply(int style, bool dark)
    {
        var palette = Palettes[Math.Clamp(style, 0, Styles.Length - 1), dark ? 1 : 0];
        var resources = Application.Current.Resources;
        Set(resources, "PageBrush", palette.Background);
        Set(resources, "PageDeepBrush", palette.BgDeep);
        Set(resources, "PaperBrush", palette.Paper);
        Set(resources, "InkBrush", palette.Ink);
        Set(resources, "MutedBrush", palette.Muted);
        Set(resources, "LineBrush", palette.Line);
        Set(resources, "EdgeBrush", palette.Edge);
        Set(resources, "PrimaryBrush", palette.Primary);
        Set(resources, "PrimaryTextBrush", palette.OnPrimary);
        Set(resources, "ActionBrush", palette.Action);
        Set(resources, "ActionTextBrush", palette.OnAction);
        Set(resources, "SoftBrush", palette.Soft);
        Set(resources, "SuccessBrush", palette.Success);
        Set(resources, "SuccessSoftBrush", palette.SuccessSoft);
        Set(resources, "WarningBrush", palette.Warning);
        Set(resources, "WarningSoftBrush", palette.WarningSoft);
        Set(resources, "InfoBrush", palette.Info);
        Set(resources, "InfoSoftBrush", palette.InfoSoft);
        Set(resources, "HoverBrush", palette.Hover);
        Set(resources, "OverlayBrush", palette.Overlay);
        Set(resources, "DangerSoftBrush", palette.DangerSoft);
    }

    private static void Set(ResourceDictionary resources, string key, string color) =>
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
