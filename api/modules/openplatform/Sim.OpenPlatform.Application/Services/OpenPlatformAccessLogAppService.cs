using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain.Shared;
using Sim.OpenPlatform.SqlSugar.Repositories;
using SqlSugar;

namespace Sim.OpenPlatform.Application.Services;

/// <summary>
/// 开放平台访问日志 — 查询令牌签发审计记录，便于追踪第三方接入。
/// 只读：审计记录由令牌端点写入，管理端不做修改。
/// </summary>
[Authorize]
[Permission(OpenPlatformPermissions.ViewAccessLogs)]
public class OpenPlatformAccessLogAppService : ApplicationService, IOpenPlatformAccessLogAppService
{
    private readonly IOpenPlatformAccessLogRepository _repository;

    public OpenPlatformAccessLogAppService(IOpenPlatformAccessLogRepository repository)
    {
        _repository = repository;
    }

    /// <summary>分页查询访问日志（令牌签发审计记录）。</summary>
    /// <param name="input">查询条件：可按 AppId、签发结果、客户端 IP、时间范围过滤，并支持分页与排序。</param>
    /// <returns>访问日志的分页结果。</returns>
    public async Task<PagedResultDto<OpenPlatformAccessLogDto>> GetListAsync(OpenPlatformAccessLogQueryDto input)
    {
        var query = _repository.GetQueryable();

        if (!string.IsNullOrWhiteSpace(input.AppId))
        {
            var appId = input.AppId.Trim();
            query = query.Where(log => log.AppId == appId);
        }

        if (input.Succeeded is { } succeeded)
        {
            query = query.Where(log => log.Succeeded == succeeded);
        }

        if (!string.IsNullOrWhiteSpace(input.ClientIp))
        {
            var clientIp = input.ClientIp.Trim();
            query = query.Where(log => log.ClientIp != null && log.ClientIp.Contains(clientIp));
        }

        if (input.StartTime is { } startTime)
        {
            query = query.Where(log => log.CreationTime >= startTime);
        }

        if (input.EndTime is { } endTime)
        {
            query = query.Where(log => log.CreationTime <= endTime);
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderBy(log => log.CreationTime, OrderByType.Desc)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var items = logs
            .Select(log => new OpenPlatformAccessLogDto
            {
                Id = log.Id,
                ApplicationId = log.ApplicationId,
                AppId = log.AppId,
                Succeeded = log.Succeeded,
                Reason = log.Reason,
                ClientIp = log.ClientIp,
                CreationTime = log.CreationTime
            })
            .ToList();

        return new PagedResultDto<OpenPlatformAccessLogDto>(totalCount, items);
    }
}
