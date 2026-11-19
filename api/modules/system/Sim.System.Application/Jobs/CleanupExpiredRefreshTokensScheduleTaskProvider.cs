using Sim.System.Application.Contracts.ScheduleTask;

namespace Sim.System.Application.Jobs;

public sealed class CleanupExpiredRefreshTokensScheduleTaskProvider :
    IScheduleTaskDefinitionProvider
{
    public IReadOnlyCollection<ScheduleTaskDefinition> GetDefinitions() =>
    [
        new(
            "CleanupExpiredRefreshTokensJob",
            "RBAC",
            "清理过期刷新令牌",
            "删除已经过期的 Refresh Token",
            typeof(CleanupExpiredRefreshTokensJob),
            "0 0 2 * * ?")
    ];
}
