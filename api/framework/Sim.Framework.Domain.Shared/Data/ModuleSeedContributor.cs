namespace Sim.Framework.Domain.Shared.Data;

/// <summary>模块种子描述：功能模块清单中的一行。</summary>
public sealed record ModuleSeed(
    string Name,
    string Description,
    string? Icon,
    int Order,
    string Layout,
    string? EmptyRedirect = null,
    bool DefaultForUserRole = false);

/// <summary>模块贡献的权限定义。</summary>
public sealed record PermissionSeed(
    string Code,
    string Name,
    string Description);

/// <summary>
/// 模块贡献的路由定义。ParentName 为空表示模块根路由；
/// 子路由按 ParentName 挂到同一模块的根路由下。
/// </summary>
public sealed record RouteSeed(
    string ModuleName,
    string Name,
    string Path,
    string Component,
    string? ParentName,
    string Title,
    string? Icon,
    int Order,
    bool Menu,
    bool Tab,
    bool KeepAlive,
    string? PermissionCode = null,
    string? Redirect = null);

/// <summary>
/// 功能模块自描述契约。每个模块提供自己的功能模块、权限与路由，
/// 平台侧只负责汇总写入，不感知具体模块。
/// </summary>
public interface IModuleSeedContributor
{
    IReadOnlyList<ModuleSeed> Modules { get; }

    IReadOnlyList<PermissionSeed> Permissions { get; }

    IReadOnlyList<RouteSeed> Routes { get; }
}
