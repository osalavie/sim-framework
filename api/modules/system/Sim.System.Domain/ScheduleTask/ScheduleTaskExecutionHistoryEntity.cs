using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.ScheduleTask;

/// <summary>
/// Tracks execution history of schedule tasks for auditing and debugging.
/// </summary>
[SugarTable("system_schedule_task_execution_history")]
[CodeFirst]
public class ScheduleTaskExecutionHistoryEntity : CreationAuditedEntity
{
    /// <summary>FK to the schedule task configuration.</summary>
    [SugarColumn(IsNullable = false)]
    public Guid ScheduleTaskId { get; set; }

    /// <summary>Task group/key for human-friendly identification.</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string TaskGroup { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = false)]
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>When the execution started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the execution completed (null if still running).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Execution result: Success, Failed, Timeout, Cancelled.</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Error message if failed.</summary>
    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? Error { get; set; }

    /// <summary>Node/instance that executed this task (for clustered environments).</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ExecutedBy { get; set; }

    /// <summary>Trigger type (Cron, Manual, Retry).</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string TriggerType { get; set; } = string.Empty;
}
