using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Sim.Admin.Application.Contracts.Localization;
using Sim.Admin.Domain.Shared.Localization;
using Sim.Framework.Application.Contracts.Localization;
using Sim.System.Application.SystemConfig;
using Sim.System.Domain.Shared.Consts;

namespace Sim.Admin.Application.Localization;

/// <summary>
/// Exposes only server-owned client resources. Static component copy remains in the Web project.
/// </summary>
public sealed class ClientLocalizationService(
    IStringLocalizer<AdminResource> localizer,
    ISystemConfigurationReader configurationReader,
    IEnumerable<IClientLocalizationContributor> contributors) : AdminAppService, IClientLocalizationService
{
    /// <summary>
    /// 获取客户端本地化资源（当前文化、站点配置与本地化文本）。
    /// </summary>
    /// <returns>客户端本地化资源</returns>
    [AllowAnonymous]
    public async Task<ClientLocalizationDto> GetAsync()
    {
        var texts = ClientLocalizationKeys.All.ToDictionary(
            key => key,
            key => localizer[key].Value,
            StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            foreach (var (key, value) in contributor.GetTexts())
            {
                texts[key] = value;
            }
        }

        var settings = await configurationReader.GetValuesAsync(
            SystemConfigurationKeys.SiteName,
            SystemConfigurationKeys.SiteDefaultLanguage,
            SystemConfigurationKeys.UiDefaultTheme,
            SystemConfigurationKeys.RegistrationEnabled);

        var siteName = settings.GetValueOrDefault(SystemConfigurationKeys.SiteName);
        if (string.IsNullOrWhiteSpace(siteName))
        {
            siteName = BrandDefaults.Name;
        }

        return new ClientLocalizationDto
        {
            Culture = CultureInfo.CurrentUICulture.Name,
            Site = new ClientSiteConfigurationDto
            {
                Name = siteName,
                DefaultLanguage = settings.GetValueOrDefault(
                    SystemConfigurationKeys.SiteDefaultLanguage) ?? "zh-Hans",
                DefaultTheme = settings.GetValueOrDefault(
                    SystemConfigurationKeys.UiDefaultTheme) ?? "auto",
                RegistrationEnabled = bool.TryParse(
                    settings.GetValueOrDefault(SystemConfigurationKeys.RegistrationEnabled),
                    out var registrationEnabled) && registrationEnabled
            },
            Texts = texts
        };
    }
}
