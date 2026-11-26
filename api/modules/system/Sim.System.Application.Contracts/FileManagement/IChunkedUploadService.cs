using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.FileManagement;

/// <summary>
/// Chunked / resumable upload service. Clients call Init → UploadChunk (repeatedly) → Complete (or Abort).
/// Chunks are stored as temporary files on disk and merged on completion.
/// </summary>
public interface IChunkedUploadService
{
    /// <summary>Initialize a new chunked upload session.</summary>
    Task<ChunkUploadSessionDto> InitSessionAsync(
        ChunkUploadInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>Upload a single chunk. Returns the updated session progress.</summary>
    Task<ChunkUploadProgressDto> UploadChunkAsync(
        ChunkUploadChunkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Merge all uploaded chunks and persist the completed file.</summary>
    Task<ChunkUploadCompletionDto> CompleteUploadAsync(
        Guid sessionId, string? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Abort an upload session and clean up temporary chunks.</summary>
    Task AbortUploadAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Clean up expired sessions (called by background job).</summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class ChunkUploadInitRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long TotalSize { get; set; }
}

public sealed class ChunkUploadSessionDto
{
    public Guid SessionId { get; set; }
    public int ChunkSize { get; set; }
    public int TotalChunks { get; set; }
    public long TotalSize { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class ChunkUploadChunkRequest
{
    public Guid SessionId { get; set; }
    public int ChunkIndex { get; set; }
    public Stream Content { get; set; } = Stream.Null;
}

public sealed class ChunkUploadProgressDto
{
    public Guid SessionId { get; set; }
    public int CompletedChunks { get; set; }
    public int TotalChunks { get; set; }
    public ChunkUploadStatus Status { get; set; }
    public bool IsComplete => CompletedChunks == TotalChunks && Status == ChunkUploadStatus.Uploading;
}

public sealed class ChunkUploadCompletionDto
{
    public string Provider { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string? Sha256 { get; set; }
}
