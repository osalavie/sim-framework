using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.IServices;
using Sim.System.FileManagement;

namespace Sim.System.FileManagement;

public interface IFileManagementService : IApplicationService
{
    Task<StoredFileDto> UploadAsync(
        IRemoteStreamContent file,
        string? key = null,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> GetDownloadAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a time-limited pre-signed access URL for direct download.
    /// S3/COS/OSS: native pre-signed URL. Local/Database: HMAC-signed server URL.
    /// </summary>
    Task<FileAccessUrlDto> GetDownloadUrlAsync(
        string key,
        string? provider = null,
        TimeSpan? expiresIn = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> RenderTemplateAsync(
        TemplateRenderInput input,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> ExportAsync(
        TabularExportInput input,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> ConvertToPdfAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default);

    Task<IRemoteStreamContent> ProcessImageAsync(
        IRemoteStreamContent file,
        int? cropX = null,
        int? cropY = null,
        int? cropWidth = null,
        int? cropHeight = null,
        int? maxWidth = null,
        int? maxHeight = null,
        int quality = 82,
        string? outputExtension = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the status of a conversion task by ID.</summary>
    Task<ConversionStatusDto?> GetConversionStatusAsync(Guid taskId);

    /// <summary>Downloads the result of a completed conversion task.</summary>
    Task<IRemoteStreamContent> GetConversionResultAsync(Guid taskId, CancellationToken cancellationToken = default);

    // ── Chunked Upload ──────────────────────────────────

    /// <summary>Initialize a new chunked upload session.</summary>
    Task<ChunkUploadSessionDto> ChunkUploadInitAsync(
        ChunkUploadInitRequest input, CancellationToken cancellationToken = default);

    /// <summary>Upload a single chunk for a session.</summary>
    Task<ChunkUploadProgressDto> ChunkUploadChunkAsync(
        Guid sessionId, int chunkIndex, IRemoteStreamContent chunk, CancellationToken cancellationToken = default);

    /// <summary>Complete an upload and merge all chunks.</summary>
    Task<ChunkUploadCompletionDto> ChunkUploadCompleteAsync(
        Guid sessionId, string? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Abort an upload session and clean up.</summary>
    Task ChunkUploadAbortAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
