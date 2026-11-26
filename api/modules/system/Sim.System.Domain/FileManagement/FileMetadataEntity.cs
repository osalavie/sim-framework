using SqlSugar;
using Sim.System.Domain.Shared;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Cross-provider file metadata — tracks every uploaded file regardless of storage backend.
/// Enables search, audit, quarantine tracking, and TTL-based cleanup.
/// </summary>
[SugarTable("system_file_metadata")]
[SugarIndex("idx_system_file_metadata_key", nameof(StorageKey), OrderByType.Asc)]
[SugarIndex("idx_system_file_metadata_provider", nameof(Provider), OrderByType.Asc)]
[SugarIndex("idx_system_file_metadata_scan_status", nameof(ScanStatus), OrderByType.Asc)]
public sealed class FileMetadataEntity : AuditedEntity
{
    public FileMetadataEntity() { }

    public FileMetadataEntity(Guid id) : base(id) { }

    /// <summary>Storage key used by the provider (e.g. "2025/07/23/{guid}.pdf").</summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Storage provider name (e.g. "local", "s3", "tencent-cos").</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Original file name supplied by the uploader.</summary>
    [SugarColumn(Length = 255, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>IANA media type (e.g. "application/pdf").</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>File size in bytes.</summary>
    [SugarColumn(IsNullable = false)]
    public long Length { get; set; }

    /// <summary>SHA-256 hex digest of the file content.</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Sha256 { get; set; }

    /// <summary>Current quarantine / scan status.</summary>
    [SugarColumn(IsNullable = false)]
    public QuarantineStatus ScanStatus { get; set; } = QuarantineStatus.Pending;

    /// <summary>Name of the scanner that last processed this file (null if never scanned).</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ScannerName { get; set; }

    /// <summary>Human-readable scan result or infection name.</summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? ScanResult { get; set; }

    /// <summary>When the scan completed (null if not yet scanned).</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ScannedAt { get; set; }
}
