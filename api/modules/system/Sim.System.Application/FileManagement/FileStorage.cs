using Sim.System.Application.Internal;
using Sim.System.FileManagement;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.FileManagement;

public sealed class FileStorage(
    IEnumerable<IFileStorageProvider> providers,
    IFileManagementOptionsProvider optionsProvider) : IFileStorage
{
    private readonly IReadOnlyDictionary<string, IFileStorageProvider> _providers = providers
        .ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
    public async Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request with { Key = FileKey.Normalize(request.Key) };
        var resolved = await ResolveAsync(provider, cancellationToken);
        return await resolved.SaveAsync(normalizedRequest, cancellationToken);
    }

    public async Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(provider, cancellationToken);
        return await resolved.OpenReadAsync(FileKey.Normalize(key), cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(provider, cancellationToken);
        return await resolved.ExistsAsync(FileKey.Normalize(key), cancellationToken);
    }

    public async Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(provider, cancellationToken);
        return await resolved.GenerateAccessUrlAsync(
            FileKey.Normalize(key),
            expiresIn,
            cancellationToken);
    }

    public async Task DeleteAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(provider, cancellationToken);
        await resolved.DeleteAsync(FileKey.Normalize(key), cancellationToken);
    }

    public async Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(provider, cancellationToken);
        return await resolved.GenerateUploadUrlAsync(
            FileKey.Normalize(key),
            fileName,
            contentType,
            contentLength,
            expiresIn,
            cancellationToken);
    }

    private async Task<IFileStorageProvider> ResolveAsync(
        string? provider,
        CancellationToken cancellationToken)
    {
        var options = await optionsProvider.GetAsync(cancellationToken);
        var name = string.IsNullOrWhiteSpace(provider)
            ? options.DefaultStorageProvider
            : provider;
        if (_providers.TryGetValue(name, out var resolved) && IsEnabled(options, name))
        {
            return resolved;
        }

        throw new InvalidOperationException(
            $"File storage provider '{name}' is disabled or not registered. Registered providers: {string.Join(", ", _providers.Keys)}.");
    }

    private static bool IsEnabled(FileManagementOptions options, string name) => name.ToLowerInvariant() switch
    {
        FileStorageProviderNames.Local => options.Local.Enabled,
        FileStorageProviderNames.Database => options.Database.Enabled,
        FileStorageProviderNames.S3 => options.S3.Enabled,
        FileStorageProviderNames.TencentCos => options.TencentCos.Enabled,
        FileStorageProviderNames.AliyunOss => options.AliyunOss.Enabled,
        _ => false
    };
}
