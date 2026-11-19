using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Sim.Admin.Application.Contracts.Localization;
using Sim.Admin.Domain.Shared.Localization;
using Sim.Framework.Application.Contracts.Localization;
using Sim.System.Application.SystemConfig;

namespace Sim.Admin.Application.Localization;

/// <summary>
/// Exposes only server-owned client resources. Static component copy remains in the Web project.
/// </summary>
public sealed class ClientLocalizationService(
    IStringLocalizer<AdminResource> localizer,
    IConfiguration configuration,
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
            "site.name",
            "site.default-language",
            "ui.default-theme",
            "security.registration-enabled");

        var siteName = settings.GetValueOrDefault("site.name");
        if (string.IsNullOrWhiteSpace(siteName))
        {
            siteName = configuration["Branding:Name"];
        }
        if (string.IsNullOrWhiteSpace(siteName))
        {
            siteName = localizer["AppName"].Value;
        }

        return new ClientLocalizationDto
        {
            Culture = CultureInfo.CurrentUICulture.Name,
            Site = new ClientSiteConfigurationDto
            {
                Name = siteName,
                DefaultLanguage = settings.GetValueOrDefault("site.default-language") ?? "zh-Hans",
                DefaultTheme = settings.GetValueOrDefault("ui.default-theme") ?? "auto",
                RegistrationEnabled = bool.TryParse(
                    settings.GetValueOrDefault("security.registration-enabled"),
                    out var registrationEnabled) && registrationEnabled
            },
            Texts = texts
        };
    }
}
