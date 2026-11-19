using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Sim.System.Domain.Security;

namespace Sim.System.Application.Jobs;

/// <summary>
/// 清理过期的 Refresh Token 定时任务
/// </summary>
[DisallowConcurrentExecution]
public class CleanupExpiredRefreshTokensJob : IJob
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<CleanupExpiredRefreshTokensJob> _logger;

    public CleanupExpiredRefreshTokensJob(
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<CleanupExpiredRefreshTokensJob> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("开始清理过期的 Refresh Token...");

            // 单条 DELETE 语句原子删除所有过期 token，无需显式事务
            var deletedCount = await _refreshTokenRepository.DeleteAsync(
                t => t.ExpiresAt < DateTime.UtcNow
            );

            if (deletedCount == 0)
            {
                _logger.LogInformation("没有需要清理的过期 Refresh Token");
                return;
            }

            _logger.LogInformation("成功清理 {Count} 个过期的 Refresh Token", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期 Refresh Token 时发生错误");
            throw;
        }
    }
}
