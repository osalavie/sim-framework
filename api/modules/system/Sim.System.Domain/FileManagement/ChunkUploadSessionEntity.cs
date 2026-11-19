using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Tracks a chunked upload session. Each session represents one file being uploaded in parts.
/// Chunks are stored as temporary files on disk; this entity tracks progress and expiry.
/// </summary>
[SugarTable("system_chunk_upload_session")]
[SugarIndex("idx_system_chunk_session_status", nameof(Status), OrderByType.Asc)]
public sealed class ChunkUploadSessionEntity : Entity
{
    public ChunkUploadSessionEntity() { }

    public ChunkUploadSessionEntity(Guid id) : base(id) { }

    /// <summary>Original file name being uploaded.</summary>
    [SugarColumn(Length = 255, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>IANA media type.</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Total expected file size in bytes.</summary>
    [SugarColumn(IsNullable = false)]
    public long TotalSize { get; set; }

    /// <summary>Maximum size of each individual chunk in bytes.</summary>
    [SugarColumn(IsNullable = false)]
    public int ChunkSize { get; set; }

    /// <summary>Total number of chunks expected.</summary>
    [SugarColumn(IsNullable = false)]
    public int TotalChunks { get; set; }

    /// <summary>Number of chunks received so far.</summary>
    [SugarColumn(IsNullable = false)]
    public int CompletedChunks { get; set; }

    /// <summary>Session status.</summary>
    [SugarColumn(IsNullable = false)]
    public ChunkUploadStatus Status { get; set; } = ChunkUploadStatus.Pending;

    /// <summary>Temporary directory path where chunks are stored.</summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string TempDirectory { get; set; } = string.Empty;

    /// <summary>When this session was created.</summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this session expires (typically 24h after creation).</summary>
    [SugarColumn(IsNullable = false)]
    public DateTime ExpiresAt { get; set; }

    /// <summary>SHA-256 digest computed during chunk upload (optional).</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Sha256 { get; set; }
}
