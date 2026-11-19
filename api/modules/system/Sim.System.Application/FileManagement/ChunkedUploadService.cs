using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// Chunked upload service that handles file upload in parts.
/// Chunks are stored as temporary files on disk and merged on completion.
/// Registered via standard DI in SystemApplicationModule.
/// </summary>
public sealed class ChunkedUploadService(
    IFileStorage fileStorage,
    ISqlSugarRepository<ChunkUploadSessionEntity> sessionRepository,
    ILogger<ChunkedUploadService> logger) : IChunkedUploadService
{
    private const int DefaultChunkSize = 5 * 1024 * 1024; // 5 MB
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    public async Task<ChunkUploadSessionDto> InitSessionAsync(
        ChunkUploadInitRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new BusinessException(SystemErrorCodes.InvalidFileName);
        if (request.TotalSize <= 0)
            throw new BusinessException(SystemErrorCodes.InvalidFileSize);

        var sessionId = Guid.NewGuid();
        var chunkSize = DefaultChunkSize;
        var totalChunks = (int)Math.Ceiling((double)request.TotalSize / chunkSize);

        var tempDir = Path.Combine(Path.GetTempPath(), "sim-chunks", sessionId.ToString("N"));
        Directory.CreateDirectory(tempDir);

        var session = new ChunkUploadSessionEntity(sessionId)
        {
            FileName = SanitizeFileName(request.FileName),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            TotalSize = request.TotalSize,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks,
            Status = ChunkUploadStatus.Pending,
            TempDirectory = tempDir,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(SessionTtl)
        };

        await sessionRepository.InsertAsync(session);

        logger.LogInformation("Chunk upload session {SessionId} created: {FileName} ({TotalSize} bytes, {TotalChunks} chunks)",
            sessionId, request.FileName, request.TotalSize, totalChunks);

        return new ChunkUploadSessionDto
        {
            SessionId = sessionId,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks,
            TotalSize = request.TotalSize,
            ExpiresAt = session.ExpiresAt
        };
    }

    public async Task<ChunkUploadProgressDto> UploadChunkAsync(
        ChunkUploadChunkRequest request, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetAsync(request.SessionId);
        if (session == null)
            throw new BusinessException(SystemErrorCodes.ChunkedSessionNotFound);

        if (session.Status is ChunkUploadStatus.Completed or ChunkUploadStatus.Aborted or ChunkUploadStatus.Expired)
            throw new BusinessException(SystemErrorCodes.ChunkedSessionClosed)
                .WithData("Status", session.Status.ToString());

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = ChunkUploadStatus.Expired;
            await sessionRepository.UpdateAsync(session);
            throw new BusinessException(SystemErrorCodes.ChunkedSessionExpired);
        }

        if (request.ChunkIndex < 0 || request.ChunkIndex >= session.TotalChunks)
            throw new BusinessException(SystemErrorCodes.InvalidChunkIndex)
                .WithData("Index", request.ChunkIndex)
                .WithData("Total", session.TotalChunks);

        var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{request.ChunkIndex:D6}");

        try
        {
            await using var chunkFile = File.Create(chunkPath);
            await request.Content.CopyToAsync(chunkFile, cancellationToken);
        }
        catch (Exception ex)
        {
            // Clean up partial chunk file
            try { File.Delete(chunkPath); } catch { /* best-effort */ }
            throw new BusinessException(SystemErrorCodes.ChunkedWriteFailed, innerException: ex);
        }

        if (session.Status == ChunkUploadStatus.Pending)
        {
            session.Status = ChunkUploadStatus.Uploading;
        }

        session.CompletedChunks++;
        await sessionRepository.UpdateAsync(session);

        logger.LogDebug("Chunk {ChunkIndex} uploaded for session {SessionId} ({Completed}/{Total})",
            request.ChunkIndex, request.SessionId, session.CompletedChunks, session.TotalChunks);

        return new ChunkUploadProgressDto
        {
            SessionId = session.Id,
            CompletedChunks = session.CompletedChunks,
            TotalChunks = session.TotalChunks,
            Status = session.Status
        };
    }

    public async Task<ChunkUploadCompletionDto> CompleteUploadAsync(
        Guid sessionId, string? provider = null, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetAsync(sessionId);
        if (session == null)
            throw new BusinessException(SystemErrorCodes.ChunkedSessionNotFound);

        if (session.Status is ChunkUploadStatus.Completed or ChunkUploadStatus.Aborted or ChunkUploadStatus.Expired)
            throw new BusinessException(SystemErrorCodes.ChunkedSessionClosed)
                .WithData("Status", session.Status.ToString());

        if (session.CompletedChunks != session.TotalChunks)
            throw new BusinessException(SystemErrorCodes.ChunkedNotComplete)
                .WithData("Completed", session.CompletedChunks)
                .WithData("Total", session.TotalChunks);

        using var mergedStream = await MergeChunksAsync(session, cancellationToken);
        var sha256 = ComputeSha256(mergedStream);
        mergedStream.Position = 0;

        var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{Path.GetExtension(session.FileName)}";

        var writeResult = await fileStorage.SaveAsync(
            new FileStorageWriteRequest(key, session.FileName, session.ContentType, mergedStream, session.TotalSize, sha256),
            provider,
            cancellationToken);

        session.Status = ChunkUploadStatus.Completed;
        session.Sha256 = sha256;
        await sessionRepository.UpdateAsync(session);

        // Clean up temp chunk files
        CleanupTempDirectory(session.TempDirectory);

        logger.LogInformation("Chunk upload session {SessionId} completed: {Key} ({Size} bytes, SHA256={Sha256})",
            sessionId, key, writeResult.Length, sha256);

        return new ChunkUploadCompletionDto
        {
            Provider = writeResult.Provider,
            Key = writeResult.Key,
            FileName = writeResult.FileName,
            ContentType = writeResult.ContentType,
            Length = writeResult.Length,
            Sha256 = writeResult.Sha256
        };
    }

    public async Task AbortUploadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetAsync(sessionId);
        if (session == null) return;

        if (session.Status is ChunkUploadStatus.Completed)
            throw new BusinessException(SystemErrorCodes.ChunkedAlreadyCompleted);

        session.Status = ChunkUploadStatus.Aborted;
        await sessionRepository.UpdateAsync(session);

        CleanupTempDirectory(session.TempDirectory);

        logger.LogInformation("Chunk upload session {SessionId} aborted", sessionId);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expired = await sessionRepository.GetListAsync(
            s => s.ExpiresAt < DateTime.UtcNow && s.Status != ChunkUploadStatus.Completed && s.Status != ChunkUploadStatus.Aborted);

        var count = 0;
        foreach (var session in expired)
        {
            session.Status = ChunkUploadStatus.Expired;
            await sessionRepository.UpdateAsync(session);
            CleanupTempDirectory(session.TempDirectory);
            count++;
        }

        if (count > 0)
            logger.LogInformation("Cleaned up {Count} expired chunk upload sessions", count);

        return count;
    }

    private static async Task<Stream> MergeChunksAsync(ChunkUploadSessionEntity session, CancellationToken cancellationToken)
    {
        var mergedPath = Path.Combine(session.TempDirectory, "merged");
        await using var merged = File.Create(mergedPath);

        for (int i = 0; i < session.TotalChunks; i++)
        {
            var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{i:D6}");
            if (!File.Exists(chunkPath))
            throw new BusinessException(SystemErrorCodes.ChunkedMissingChunk)
                    .WithData("ChunkIndex", i);

            await using var chunkFile = File.OpenRead(chunkPath);
            await chunkFile.CopyToAsync(merged, cancellationToken);
        }

        await merged.FlushAsync(cancellationToken);

        // Restore position for reading hash
        return File.OpenRead(mergedPath);
    }

    private static string ComputeSha256(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    private void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup — log but don't throw
            logger.LogWarning(ex, "Failed to clean up temp directory {TempPath}", path);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var normalized = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(normalized) ? "file.bin" : normalized;
    }
}
