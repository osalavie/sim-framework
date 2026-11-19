using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Tracks every file download event across all storage providers.
/// For Local/Database providers: recorded by the server access endpoint after streaming completes.
/// For S3/COS/OSS: recorded via object storage event notification webhooks.
/// </summary>
[SugarTable("system_file_download_record")]
[SugarIndex("idx_system_fdr_metadata", nameof(FileMetadataId), OrderByType.Asc)]
[SugarIndex("idx_system_fdr_started", nameof(StartedAt), OrderByType.Desc)]
public sealed class FileDownloadRecordEntity : CreationAuditedEntity
{
    public FileDownloadRecordEntity() { }

    public FileDownloadRecordEntity(Guid id) : base(id) { }

    /// <summary>FK to FileMetadataEntity.Id (nullable — direct access URLs may not reference metadata).</summary>
    [SugarColumn(IsNullable = true)]
    public Guid? FileMetadataId { get; set; }

    /// <summary>Storage provider name.</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Storage key of the downloaded file.</summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>When the download started.</summary>
    [SugarColumn(IsNullable = false)]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the download completed (null if still in progress or failed).</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total bytes transferred (set on completion).</summary>
    [SugarColumn(IsNullable = true)]
    public long? BytesTransferred { get; set; }

    /// <summary>Client IP address that initiated the download.</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ClientIp { get; set; }

    /// <summary>User-Agent header of the download request.</summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? UserAgent { get; set; }

    /// <summary>Download status.</summary>
    [SugarColumn(IsNullable = false)]
    public DownloadStatus Status { get; set; } = DownloadStatus.Started;

    /// <summary>Error message if the download failed.</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Error { get; set; }
}
