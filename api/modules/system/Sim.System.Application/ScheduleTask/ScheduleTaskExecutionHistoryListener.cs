using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Sim.Framework.Domain.Shared.Telemetry;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.ScheduleTask;

namespace Sim.System.Application.ScheduleTask;

/// <summary>
/// Quartz job listener that writes execution history to
/// <see cref="ScheduleTaskExecutionHistoryEntity"/> for every scheduled task.
/// Uses IServiceScopeFactory to resolve repositories lazily during execution.
/// </summary>
public sealed class ScheduleTaskExecutionHistoryListener(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduleTaskExecutionHistoryListener> logger) : IJobListener
{
    public string Name => nameof(ScheduleTaskExecutionHistoryListener);

    private const string HistoryIdKey = "Sim.System.ExecutionHistoryId";

    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var configRepo = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<ScheduleTaskConfigurationEntity>>();
        var historyRepo = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<ScheduleTaskExecutionHistoryEntity>>();

        var config = await ResolveConfigurationAsync(context, configRepo);
        if (config == null)
        {
            return;
        }

        var triggerType = ResolveTriggerType(context);
        var executedBy = Environment.MachineName;

        var history = new ScheduleTaskExecutionHistoryEntity
        {
            ScheduleTaskId = config.Id,
            TaskGroup = config.Group,
            TaskKey = config.Key,
            StartedAt = context.FireTimeUtc,
            Status = "Running",
            TriggerType = triggerType,
            ExecutedBy = executedBy
        };

        try
        {
            await historyRepo.InsertAsync(history);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to insert execution history record for task {TaskKey}", config.Key);
            return;
        }

        context.Put(HistoryIdKey, history.Id);
    }

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        RecordExecutionMetrics(context, jobException);

        var historyId = context.Get(HistoryIdKey) as Guid?;
        if (historyId == null || historyId == Guid.Empty)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var historyRepo = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<ScheduleTaskExecutionHistoryEntity>>();
            var history = await historyRepo.GetAsync(historyId.Value);
            if (history == null)
            {
                return;
            }

            history.CompletedAt = DateTimeOffset.UtcNow;
            history.DurationMs = (long)context.JobRunTime.TotalMilliseconds;
            history.Status = jobException == null ? "Success" : "Failed";
            history.Error = jobException?.Message?.Truncate(4000);

            await historyRepo.UpdateAsync(history);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update execution history record {HistoryId}", historyId);
        }
    }

    public Task JobExecutionVetoed(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 记录定时任务指标。执行历史可能因配置缺失或写库失败而跳过，
    /// 指标仍需反映真实执行结果，因此独立于历史记录逻辑。
    /// 标签只使用有界的任务键与触发方式，不写入任务参数等自由文本。
    /// </summary>
    private static void RecordExecutionMetrics(IJobExecutionContext context, JobExecutionException? jobException)
    {
        var jobKey = context.JobDetail.Key;
        var tags = new TagList
        {
            { Metrics.Tags.TaskGroup, jobKey.Group },
            { Metrics.Tags.TaskKey, jobKey.Name },
            { Metrics.Tags.TriggerType, ResolveTriggerType(context) },
            { Metrics.Tags.Status, jobException == null ? Metrics.Status.Success : Metrics.Status.Failure },
        };

        Metrics.ScheduleTaskCount.Add(1, tags);
        Metrics.ScheduleTaskDuration.Record(context.JobRunTime.TotalMilliseconds, tags);
    }

    /// <summary>手工触发（手动触发器）与 Cron 触发通过触发器名前缀区分。</summary>
    private static string ResolveTriggerType(IJobExecutionContext context)
        => context.Trigger?.Key.Name?.StartsWith("MT_") == true
            ? Metrics.TriggerTypes.Manual
            : Metrics.TriggerTypes.Cron;

    private static async Task<ScheduleTaskConfigurationEntity?> ResolveConfigurationAsync(
        IJobExecutionContext context,
        ISqlSugarRepository<ScheduleTaskConfigurationEntity> configRepo)
    {
        // Dynamic tasks: config ID stored in JobDataMap.
        // TryGetString avoids KeyNotFoundException when the key is absent,
        // so built-in tasks (which don't carry this key) fall through to the
        // JobKey-based lookup below.
        if (context.MergedJobDataMap.TryGetString(
                DynamicScheduleTaskJob.ConfigurationIdKey,
                out var idValue)
            && !string.IsNullOrWhiteSpace(idValue)
            && Guid.TryParse(idValue, out var id))
        {
            return await configRepo.GetAsync(id);
        }

        // Built-in tasks: match by JobKey (Group = task group, Name = task key)
        var jobKey = context.JobDetail.Key;
        var configs = await configRepo.GetListAsync(c =>
            c.Group == jobKey.Group && c.Key == jobKey.Name);
        return configs.FirstOrDefault();
    }
}

internal static class StringExtensions
{
    public static string? Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
