using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Sim.Admin.Application.Contracts.SiteStatus;
using Sim.Framework.Application.Services;

namespace Sim.Admin.Application.SiteStatus;

/// <summary>
/// Exposes the current deployment maintenance state. It stays reachable while other API requests are blocked.
/// </summary>
public class SiteStatusService(IConfiguration configuration) : AdminAppService, ISiteStatusService
{
    /// <summary>
    /// 获取当前站点运行状态（在其它 API 请求被拦截时仍保持可访问）。
    /// </summary>
    /// <returns>当前站点运行状态</returns>
    [AllowAnonymous]
    public Task<SiteStatusDto> GetAsync()
    {
        var maintenanceMode = configuration.GetValue("Site:MaintenanceMode", false);
        var maintenanceMessage = configuration["Site:MaintenanceMessage"];

        return Task.FromResult(new SiteStatusDto
        {
            MaintenanceMode = maintenanceMode,
            MaintenanceMessage = string.IsNullOrWhiteSpace(maintenanceMessage)
                ? null
                : maintenanceMessage
        });
    }
}
