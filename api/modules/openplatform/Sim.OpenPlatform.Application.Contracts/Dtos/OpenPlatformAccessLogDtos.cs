using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts.Dtos;

/// <summary>
/// 开放平台访问日志（令牌签发审计）查询条件。
/// </summary>
public class OpenPlatformAccessLogQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>按应用 AppId 精确过滤。</summary>
    public string? AppId { get; set; }

    /// <summary>按签发结果过滤：true 仅成功，false 仅失败，null 表示全部。</summary>
    public bool? Succeeded { get; set; }

    /// <summary>按客户端 IP 模糊过滤。</summary>
    public string? ClientIp { get; set; }

    /// <summary>起始时间（含）。</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间（含）。</summary>
    public DateTime? EndTime { get; set; }
}
