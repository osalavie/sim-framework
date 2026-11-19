using System.Collections.Generic;

namespace Sim.Admin.Domain.Shared.Localization;

/// <summary>
/// 后端拥有、经 <c>GET /api/admin/client-localization</c> 下发给客户端的本地化键白名单。
/// 菜单键统一登记在 <see cref="MenuLocalizationKeys"/>，品牌文案等非菜单键在此登记。
/// </summary>
public static class ClientLocalizationKeys
{
    public const string AuthBrandHeadline = "Auth:BrandHeadline";

    private static readonly IReadOnlyList<string> BrandKeys = [AuthBrandHeadline];

    /// <summary>
    /// 客户端本地化白名单：菜单键在前，品牌文案键在后。
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
        [.. MenuLocalizationKeys.ClientKeys, .. BrandKeys];
}
