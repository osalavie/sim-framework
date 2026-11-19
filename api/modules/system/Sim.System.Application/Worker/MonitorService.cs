using Microsoft.AspNetCore.Authorization;
using Quartz;
using Quartz.Impl.Matchers;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.System.Application.Contracts.Worker;

namespace Sim.System.Application.Worker;

[Authorize]
public class MonitorService : ApplicationService, IMonitorService
{
    private readonly ISchedulerFactory _schedulerFactory;

    public MonitorService(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    /// <summary>
    /// 获取所有后台任务（Worker）的运行信息列表。
    /// </summary>
    /// <returns>后台任务运行信息列表</returns>
    [Permission("system:worker:query")]
    public async Task<List<WorkerInfo>> GetWorkerInfosAsync()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var groups = await scheduler.GetJobGroupNames();
        var workerInfos = new List<WorkerInfo>();
        foreach (var group in groups)
        {
            foreach (var jobKey in await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group)))
            {
                string jobName = jobKey.Name;
                string jobGroup = jobKey.Group;
                var jobDetail = await scheduler.GetJobDetail(jobKey);
                if (jobDetail == null)
                {
                    continue;
                }
                var triggers = await scheduler.GetTriggersOfJob(jobKey);
                if (triggers.Any())
                {
                    var trigger = triggers.First();
                    var state = await scheduler.GetTriggerState(trigger.Key);
                    workerInfos.Add(new WorkerInfo
                    {
                        JobId = jobDetail.Key.Name,
                        JobName = jobDetail.Key.Group,
                        JobType = jobDetail.JobType.Name,
                        AllowConcurrent = !jobDetail.ConcurrentExecutionDisallowed,
                        Description = jobDetail.Description ?? string.Empty,
                        LastRunTime = trigger.GetPreviousFireTimeUtc()?.UtcDateTime ?? DateTime.MinValue,
                        NextRunTime = trigger.GetNextFireTimeUtc()?.UtcDateTime ?? DateTime.MinValue,
                        AssemblyName = jobDetail.JobType.Assembly.GetName().Name ?? string.Empty,
                        Status = state.ToString()
                    });
                }
            }
        }
        return workerInfos;
    }
}
