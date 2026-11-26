using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.ScheduleTask;

public sealed record ScheduleTaskDefinition(
    string Key,
    string Group,
    string DisplayName,
    string Description,
    Type JobType,
    string DefaultCronExpression,
    bool DefaultEnabled = true);

public static class ScheduleTaskTypes
{
    public const string Http = "Http";
    public const string Command = "Command";
    public const string ServiceMethod = "ServiceMethod";
    public const string Worker = "Worker";

    public static readonly string[] DynamicTypes = [Http, Command, ServiceMethod];
}

public interface IScheduleTaskDefinitionProvider
{
    IReadOnlyCollection<ScheduleTaskDefinition> GetDefinitions();
}

public interface IScheduleTaskServiceMethodHandler
{
    string Key { get; }
    string DisplayName { get; }
    string Description { get; }

    Task ExecuteAsync(string? argumentsJson, CancellationToken cancellationToken = default);
}

public sealed class ScheduleTaskServiceMethodDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ScheduleTaskDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string TaskType { get; set; } = ScheduleTaskTypes.Worker;
    public bool IsBuiltIn { get; set; }
    public bool CanDelete => !IsBuiltIn;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? PreviousRunTime { get; set; }
    public DateTimeOffset? NextRunTime { get; set; }
}

public sealed class CreateScheduleTaskDto
{
    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(32)]
    public string TaskType { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string CronExpression { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [MaxLength(16)]
    public string? HttpMethod { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }

    public Dictionary<string, string>? HttpHeaders { get; set; }

    [MaxLength(65536)]
    public string? HttpBody { get; set; }

    [MaxLength(128)]
    public string? HttpContentType { get; set; }

    [Required]
    [MaxLength(256)]
    public string? Command { get; set; }

    // Backward-compatible wire alias. It is interpreted as a command name, never as a path.
    [Obsolete("Use Command instead.")]
    [MaxLength(2048)]
    public string? ExecutablePath { get; set; }

    public List<string>? CommandArguments { get; set; }

    [MaxLength(2048)]
    public string? WorkingDirectory { get; set; }

    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 120;

    [MaxLength(256)]
    public string? ServiceMethodKey { get; set; }

    [MaxLength(65536)]
    public string? ServiceArgumentsJson { get; set; }
}

public sealed class UpdateScheduleTaskDto
{
    [Required]
    [MaxLength(128)]
    public string CronExpression { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

public interface IScheduleTaskService : IApplicationService
{
    Task<List<ScheduleTaskDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<ScheduleTaskServiceMethodDto>> GetServiceMethodsAsync(
        CancellationToken cancellationToken = default);

    Task<ScheduleTaskDto> CreateAsync(
        CreateScheduleTaskDto input,
        CancellationToken cancellationToken = default);

    Task<ScheduleTaskDto> UpdateAsync(
        string group,
        string key,
        UpdateScheduleTaskDto input,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a scheduled task immediately, outside its cron schedule.
    /// </summary>
    Task TriggerNowAsync(Guid id, CancellationToken cancellationToken = default);
}
