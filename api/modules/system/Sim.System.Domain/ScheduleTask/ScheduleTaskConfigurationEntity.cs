using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.ScheduleTask;

[CodeFirst]
[SugarTable("system_schedule_task_config")]
[SugarIndex("uidx_system_schedule_task_identity", nameof(Group), OrderByType.Asc,
    nameof(Key), OrderByType.Asc, true)]
public class ScheduleTaskConfigurationEntity : AuditedAggregateRoot
{
    public ScheduleTaskConfigurationEntity()
    {
    }

    public ScheduleTaskConfigurationEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Group { get; set; } = "System";

    [SugarColumn(Length = 256, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string CronExpression { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public bool Enabled { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string TaskType { get; set; } = "Worker";

    [SugarColumn(IsNullable = false)]
    public bool IsBuiltIn { get; set; } = true;

    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? ProtectedConfiguration { get; set; }
}
