using System.Collections.Generic;
using System.Threading.Tasks;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Admin.Application.Contracts.Localization;

/// <summary>
/// 后端拥有、供前端消费的本地化资源。
/// </summary>
public sealed class ClientLocalizationDto
{
    /// <summary>
    /// 当前请求使用的语言文化名称（如 zh-CN、en）。
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// 影响客户端展示的站点配置。
    /// </summary>
    public ClientSiteConfigurationDto Site { get; set; } = new();

    /// <summary>
    /// 本地化文本字典（key 为资源名，value 为当前文化的翻译文本）。
    /// </summary>
    public Dictionary<string, string> Texts { get; set; } = [];
}

/// <summary>
/// 影响客户端展示的站点配置。
/// </summary>
public sealed class ClientSiteConfigurationDto
{
    /// <summary>
    /// 站点名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 站点默认语言（zh-Hans 或 en）。
    /// </summary>
    public string DefaultLanguage { get; set; } = "zh-Hans";

    /// <summary>
    /// 站点默认主题（auto、light 或 dark）。
    /// </summary>
    public string DefaultTheme { get; set; } = "auto";

    /// <summary>
    /// 是否开放自助注册。
    /// </summary>
    public bool RegistrationEnabled { get; set; }
}

/// <summary>
/// 客户端本地化服务接口：提供后端拥有、供前端消费的本地化资源。
/// </summary>
public interface IClientLocalizationService : IApplicationService
{
    /// <summary>
    /// 获取客户端本地化资源（当前文化、站点配置与本地化文本）。
    /// </summary>
    /// <returns>客户端本地化资源</returns>
    Task<ClientLocalizationDto> GetAsync();
}
