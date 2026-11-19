namespace Sim.System.Application.Contracts.Worker;

/// <summary>
/// 后台任务（Worker）运行信息 DTO。
/// </summary>
public class WorkerInfo
{
    /// <summary>
    /// 任务唯一标识（Quartz Job Key 的名称）。
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// 任务名称（Quartz Job Key 所属的分组名）。
    /// </summary>
    public required string JobName { get; set; }

    /// <summary>
    /// 任务类型名称。
    /// </summary>
    public required string JobType { get; set; }

    /// <summary>
    /// 是否允许并发执行。
    /// </summary>
    public bool AllowConcurrent { get; set; }

    /// <summary>
    /// 任务描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 上次执行时间。
    /// </summary>
    public DateTime LastRunTime { get; set; }

    /// <summary>
    /// 下次执行时间。
    /// </summary>
    public DateTime NextRunTime { get; set; }

    /// <summary>
    /// 任务所在程序集名称。
    /// </summary>
    public required string AssemblyName { get; set; }

    /// <summary>
    /// 任务当前状态。
    /// </summary>
    public required string Status { get; set; }
}