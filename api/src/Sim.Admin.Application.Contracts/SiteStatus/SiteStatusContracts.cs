using System.Threading.Tasks;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.Admin.Application.Contracts.SiteStatus;

/// <summary>
/// Current site runtime status exposed to Web clients.
/// </summary>
public sealed class SiteStatusDto
{
    /// <summary>
    /// Whether Web and business APIs are in maintenance mode.
    /// </summary>
    public bool MaintenanceMode { get; set; }

    /// <summary>
    /// Optional operator-provided maintenance notice.
    /// </summary>
    public string? MaintenanceMessage { get; set; }
}

/// <summary>
/// 站点状态服务接口：查询当前站点运行状态（如维护模式）。
/// </summary>
public interface ISiteStatusService : IApplicationService
{
    /// <summary>
    /// 获取当前站点运行状态。
    /// </summary>
    /// <returns>当前站点运行状态</returns>
    Task<SiteStatusDto> GetAsync();
}
