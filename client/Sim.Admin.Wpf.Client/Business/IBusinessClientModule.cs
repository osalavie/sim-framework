using System.Windows;

namespace Sim.Admin.Wpf.Client.Business;

/// <summary>业务客户端模块契约：声明菜单分组，并按需创建自己的懒加载工作区。</summary>
public interface IBusinessClientModule
{
    /// <summary>业务标识，与后端业务根路径一致，例如 blog。</summary>
    string Key { get; }

    string Title { get; }

    string Glyph { get; }

    int Order { get; }

    IReadOnlyList<BusinessMenuItem> Menus { get; }

    IBusinessWorkspace CreateWorkspace(BusinessClientContext context);
}

/// <summary>业务菜单叶节点；Key 约定为 "&lt;模块&gt;.&lt;页面&gt;"。</summary>
public sealed record BusinessMenuItem(string Key, string Title, string Glyph);

/// <summary>业务工作区：菜单切换时由模块内部完成导航。</summary>
public interface IBusinessWorkspace
{
    FrameworkElement View { get; }

    Task ShowAsync(string menuKey);
}
