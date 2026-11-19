using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.FileManagement;

/// <summary>
/// Status response for a file conversion task.
/// </summary>
public sealed class ConversionStatusDto
{
    public Guid TaskId { get; init; }
    public ConversionTaskType TaskType { get; init; }
    public ConversionTaskStatus Status { get; init; }
    public string? InputFileName { get; init; }
    public string? ResultFileName { get; init; }
    public string? Error { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
