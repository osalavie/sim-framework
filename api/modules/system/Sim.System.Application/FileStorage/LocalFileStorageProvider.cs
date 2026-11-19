using System.Security.Cryptography;
using System.Text.Json;
using Sim.System.Application.Internal;
using Sim.System.FileManagement;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.FileStorage;

public sealed class LocalFileStorageProvider(
    IFileManagementOptionsProvider optionsProvider) : IFileStorageProvider
{
    public string Name => FileStorageProviderNames.Local;

    public async Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var rootPath = await GetRootPathAsync(cancellationToken);
        var normalizedKey = FileKey.Normalize(request.Key);
        var targetPath = ResolvePath(rootPath, normalizedKey);
        var metadataPath = targetPath + ".metadata.json";
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var temporaryPath = targetPath + $".{Guid.NewGuid():N}.upload";
        try
        {
            await using (var target = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await request.Content.CopyToAsync(target, cancellationToken);
            }

            await MoveWithRetryAsync(temporaryPath, targetPath, cancellationToken);
            var metadata = new LocalFileMetadata(
                request.FileName,
                request.ContentType,
                new FileInfo(targetPath).Length,
                request.Sha256);
            await File.WriteAllTextAsync(
                metadataPath,
                JsonSerializer.Serialize(metadata),
                cancellationToken);

            return new FileStorageWriteResult(
                Name,
                normalizedKey,
                metadata.FileName,
                metadata.ContentType,
                metadata.Length,
                metadata.Sha256);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var rootPath = await GetRootPathAsync(cancellationToken);
        var normalizedKey = FileKey.Normalize(key);
        var targetPath = ResolvePath(rootPath, normalizedKey);
        if (!File.Exists(targetPath))
        {
            return null;
        }

        var metadata = await ReadMetadataAsync(targetPath, cancellationToken);
        var stream = new FileStream(
            targetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return new FileStorageReadResult(
            Name,
            normalizedKey,
            metadata.FileName,
            metadata.ContentType,
            metadata.Length,
            stream);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var rootPath = await GetRootPathAsync(cancellationToken);
        return File.Exists(ResolvePath(rootPath, FileKey.Normalize(key)));
    }

    public async Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var rootPath = await GetRootPathAsync(cancellationToken);
        var normalizedKey = FileKey.Normalize(key);
        if (!File.Exists(ResolvePath(rootPath, normalizedKey)))
        {
            return null;
        }

        var options = await optionsProvider.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
            string.IsNullOrWhiteSpace(options.AccessUrlSigningKey))
        {
            throw new InvalidOperationException(
                "FileManagement:PublicBaseUrl and FileManagement:AccessUrlSigningKey must be configured for Local file access URLs.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var token = FileAccessTokenHelper.Generate(normalizedKey, expiresAt, options.AccessUrlSigningKey);
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var expiresUnix = expiresAt.ToUnixTimeSeconds();
        var url = $"{baseUrl}/api/files/access/{Name}/{Uri.EscapeDataString(normalizedKey)}?token={Uri.EscapeDataString(token)}&expires={expiresUnix}";

        return new FileAccessUrlResult(
            Name,
            normalizedKey,
            url,
            expiresAt,
            IsExternalUrl: false);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var rootPath = await GetRootPathAsync(cancellationToken);
        var targetPath = ResolvePath(rootPath, FileKey.Normalize(key));
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        var metadataPath = targetPath + ".metadata.json";
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }
    }

    public Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        // Local provider does not support direct upload; clients must use the server-side upload endpoint.
        return Task.FromResult<FileUploadUrlResult?>(null);
    }

    private async Task<string> GetRootPathAsync(CancellationToken cancellationToken)
    {
        var options = await optionsProvider.GetAsync(cancellationToken);
        var rootPath = Path.GetFullPath(options.Local.RootPath);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private static string ResolvePath(string rootPath, string key)
    {
        var relativePath = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved file path escapes the configured local storage root.");
        }

        return fullPath;
    }

    private static async Task MoveWithRetryAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 8;
        var delay = TimeSpan.FromMilliseconds(50);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
            }
            catch (IOException) when (attempt < maxAttempts)
            {
            }

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 500));
        }
    }

    private static async Task<LocalFileMetadata> ReadMetadataAsync(
        string targetPath,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(targetPath);
        var metadataPath = targetPath + ".metadata.json";
        if (File.Exists(metadataPath))
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<LocalFileMetadata>(json);
            if (metadata != null)
            {
                return metadata with { Length = fileInfo.Length };
            }
        }

        await using var stream = fileInfo.OpenRead();
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        return new LocalFileMetadata(
            fileInfo.Name,
            "application/octet-stream",
            fileInfo.Length,
            sha256);
    }

    private sealed record LocalFileMetadata(
        string FileName,
        string ContentType,
        long Length,
        string Sha256);
}
