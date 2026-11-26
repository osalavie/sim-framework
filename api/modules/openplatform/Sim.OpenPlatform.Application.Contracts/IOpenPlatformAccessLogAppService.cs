using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;
using Sim.OpenPlatform.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台访问日志查询（只读）。
/// </summary>
public interface IOpenPlatformAccessLogAppService : IApplicationService
{
    /// <summary>
    /// 分页查询访问日志（令牌签发审计记录）。
    /// </summary>
    /// <param name="input">查询条件：可按 AppId、签发结果、客户端 IP、时间范围过滤，并支持分页与排序。</param>
    /// <returns>访问日志的分页结果。</returns>
    Task<PagedResultDto<OpenPlatformAccessLogDto>> GetListAsync(OpenPlatformAccessLogQueryDto input);
}
