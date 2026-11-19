namespace Sim.System.Application.ScheduleTask;

public sealed class ScheduleTaskExecutionConfiguration
{
    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string> HttpHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? HttpBody { get; set; }
    public string? HttpContentType { get; set; }
    public string? Command { get; set; }
    public List<string> CommandArguments { get; set; } = [];
    public string? WorkingDirectory { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public string? ServiceMethodKey { get; set; }
    public string? ServiceArgumentsJson { get; set; }
}
