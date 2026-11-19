using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Persistent record of a file conversion task.
/// Survives process restarts and enables cross-instance processing via RabbitMQ.
/// </summary>
[SugarTable("system_file_conversion_task")]
public sealed class FileConversionTaskEntity : AuditedEntity
{
    /// <summary>Conversion type: DocumentToPdf or ImageProcessing.</summary>
    public ConversionTaskType TaskType { get; set; }

    /// <summary>Original file name provided by the client.</summary>
    public string InputFileName { get; set; } = string.Empty;

    /// <summary>Storage key where the input file was temporarily saved.</summary>
    public string InputStorageKey { get; set; } = string.Empty;

    /// <summary>Storage provider for the input file (null = default).</summary>
    public string? InputProvider { get; set; }

    /// <summary>Output extension for image processing (null for PDF conversion).</summary>
    public string? OutputExtension { get; set; }

    /// <summary>Image quality (1-100), default 82.</summary>
    public int Quality { get; set; } = 82;

    /// <summary>Current status of the task.</summary>
    public ConversionTaskStatus Status { get; set; } = ConversionTaskStatus.Pending;

    /// <summary>Number of retry attempts.</summary>
    public int RetryCount { get; set; }

    /// <summary>Maximum retries before dead-letter.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>When processing started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the task reached a terminal state (Completed/Failed/DeadLetter).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }

    /// <summary>Storage key where the result file was saved.</summary>
    public string? ResultStorageKey { get; set; }

    /// <summary>Result file name.</summary>
    public string? ResultFileName { get; set; }

    /// <summary>Result content type.</summary>
    public string? ResultContentType { get; set; }

    /// <summary>Result file length in bytes.</summary>
    public long? ResultLength { get; set; }
}
