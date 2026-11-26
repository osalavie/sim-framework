using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<List<ScheduleTask>> GetScheduleTasksAsync() =>
        SendJsonAsync<List<ScheduleTask>>(HttpMethod.Get, "api/system/schedule-task");

    public Task<List<ScheduleServiceMethod>> GetScheduleServiceMethodsAsync() =>
        SendJsonAsync<List<ScheduleServiceMethod>>(HttpMethod.Get,
            "api/system/schedule-task/service-methods");

    public Task<ScheduleTask> CreateScheduleTaskAsync(ScheduleTaskCreate input) =>
        SendJsonAsync<ScheduleTask>(HttpMethod.Post, "api/system/schedule-task", input);

    public async Task UpdateScheduleTaskAsync(ScheduleTask task)
    {
        using var response = await SendAsync(HttpMethod.Put,
            Query("api/system/schedule-task", ("group", task.Group), ("key", task.Key)),
            new { cronExpression = task.CronExpression, enabled = task.Enabled });
    }

    public async Task DeleteScheduleTaskAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/system/schedule-task/{id:D}");
    }

    public async Task TriggerScheduleTaskAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/system/schedule-task/{id:D}/trigger-now");
    }
}

public sealed class ScheduleTask
{
    public Guid Id { get; set; }
    public string? Key { get; set; }
    public string? Group { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? CronExpression { get; set; }
    public bool Enabled { get; set; }
    public string? TaskType { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool CanDelete { get; set; }
    public string? Status { get; set; }
    public DateTime? PreviousRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
}

public sealed class ScheduleTaskCreate
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = "Http";
    public string CronExpression { get; set; } = "0 0/5 * * * ?";
    public bool Enabled { get; set; } = true;
    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? HttpHeaders { get; set; }
    public string? HttpBody { get; set; }
    public string? HttpContentType { get; set; }
    public string? Command { get; set; }
    public string[]? CommandArguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? ServiceMethodKey { get; set; }
    public string? ServiceArgumentsJson { get; set; }
}

public sealed class ScheduleServiceMethod
{
    public string? Key { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Key ?? string.Empty : DisplayName;
}
