using Sim.System.Application.Internal;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileStorage;

public sealed class DatabaseFileStorageProvider(
    ISqlSugarRepository<DatabaseStoredFileEntity> repository,
    IFileManagementOptionsProvider optionsProvider) : IFileStorageProvider
{
    public string Name => FileStorageProviderNames.Database;

    public async Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        var entity = await repository.GetQueryable()
            .FirstAsync(storedFile => storedFile.StorageKey == request.Key);
        if (entity == null)
        {
            entity = new DatabaseStoredFileEntity(Guid.NewGuid())
            {
                StorageKey = request.Key,
                CreationTime = DateTime.UtcNow
            };
            Apply(entity, request, content);
            await repository.InsertAsync(entity);
        }
        else
        {
            Apply(entity, request, content);
            entity.LastModificationTime = DateTime.UtcNow;
            await repository.UpdateAsync(entity);
        }

        return new FileStorageWriteResult(
            Name,
            entity.StorageKey,
            entity.FileName,
            entity.ContentType,
            entity.Length,
            entity.Sha256);
    }

    public async Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await repository.GetQueryable()
            .FirstAsync(storedFile => storedFile.StorageKey == key);
        return entity == null
            ? null
            : new FileStorageReadResult(
                Name,
                entity.StorageKey,
                entity.FileName,
                entity.ContentType,
                entity.Length,
                new MemoryStream(entity.Content, writable: false));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await repository.GetQueryable().AnyAsync(storedFile => storedFile.StorageKey == key);
    }

    public async Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await repository.GetQueryable().AnyAsync(storedFile => storedFile.StorageKey == key))
        {
            return null;
        }

        var options = await optionsProvider.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
            string.IsNullOrWhiteSpace(options.AccessUrlSigningKey))
        {
            throw new InvalidOperationException(
                "FileManagement:PublicBaseUrl and FileManagement:AccessUrlSigningKey must be configured for Database file access URLs.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var token = FileAccessTokenHelper.Generate(key, expiresAt, options.AccessUrlSigningKey);
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var expiresUnix = expiresAt.ToUnixTimeSeconds();
        var url = $"{baseUrl}/api/files/access/{Name}/{Uri.EscapeDataString(key)}?token={Uri.EscapeDataString(token)}&expires={expiresUnix}";

        return new FileAccessUrlResult(
            Name,
            key,
            url,
            expiresAt,
            IsExternalUrl: false);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await repository.DeleteAsync(storedFile => storedFile.StorageKey == key);
    }

    public Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        // Database provider does not support direct upload; clients must use the server-side upload endpoint.
        return Task.FromResult<FileUploadUrlResult?>(null);
    }

    private static void Apply(
        DatabaseStoredFileEntity entity,
        FileStorageWriteRequest request,
        byte[] content)
    {
        entity.FileName = request.FileName;
        entity.ContentType = request.ContentType;
        entity.Length = content.LongLength;
        entity.Sha256 = request.Sha256;
        entity.Content = content;
    }
}
