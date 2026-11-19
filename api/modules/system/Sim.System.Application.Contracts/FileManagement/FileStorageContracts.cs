using Sim.System.Domain.Shared;

namespace Sim.System.FileManagement;

public sealed record FileStorageWriteRequest(
    string Key,
    string FileName,
    string ContentType,
    Stream Content,
    long Length,
    string Sha256);

public sealed record FileStorageWriteResult(
    string Provider,
    string Key,
    string FileName,
    string ContentType,
    long Length,
    string Sha256);

public sealed record FileStorageReadResult(
    string Provider,
    string Key,
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

/// <summary>
/// Result of generating a time-limited access URL for a stored file.
/// For S3/COS/OSS providers this is a native pre-signed URL pointing to the object storage directly.
/// For Local/Database providers this is an HMAC-signed URL pointing to the server's file access endpoint.
/// </summary>
public sealed record FileAccessUrlResult(
    string Provider,
    string Key,
    string Url,
    DateTimeOffset ExpiresAt,
    bool IsExternalUrl);

/// <summary>
/// Result of generating a time-limited direct upload URL.
/// Only S3/COS/OSS providers support this; Local/Database return null.
/// </summary>
public sealed record FileUploadUrlResult(
    string Provider,
    string Key,
    string UploadUrl,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public interface IFileStorageProvider
{
    string Name { get; }

    Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a time-limited access URL for downloading a file without going through the server proxy.
    /// S3/COS/OSS: native pre-signed URL. Local/Database: HMAC-signed URL to the server's file access endpoint.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a time-limited direct upload URL for client-side direct-to-storage uploads.
    /// Only S3/COS/OSS providers support this; Local/Database return null.
    /// </summary>
    Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public interface IFileStorage
{
    Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a time-limited access URL for downloading a file.
    /// </summary>
    Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        string? provider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a time-limited direct upload URL for client-side direct-to-storage uploads.
    /// Returns null if the configured provider does not support direct uploads.
    /// </summary>
    Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default);
}
