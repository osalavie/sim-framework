
namespace Sim.System.Domain.Shared.Enums;

public enum ChunkUploadStatus
{
    Pending = 0,
    Uploading = 1,
    Completed = 2,
    Aborted = 3,
    Expired = 4
}

/// <summary>
/// Visibility shared by business resources and their managed files.
/// </summary>
public enum ResourceVisibility
{
    Private = 0,
    Public = 1
}

public enum TabularExportFormat
{
    Csv,
    Json,
    Html
}

public enum DownloadStatus
{
    Started = 0,
    Completed = 1,
    Failed = 2
}

public enum QuarantineStatus
{
    /// <summary>No scan required or scan not yet initiated.</summary>
    Pending = 0,
    /// <summary>Currently being scanned.</summary>
    Scanning = 1,
    /// <summary>Scan completed, file is clean.</summary>
    Clean = 2,
    /// <summary>Scan detected malicious content.</summary>
    Infected = 3,
    /// <summary>Scan failed due to scanner error.</summary>
    ScanError = 4
}

public enum ConversionTaskType
{
    DocumentToPdf,
    ImageProcessing
}

public enum ConversionTaskStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    DeadLetter
}
