using System.Net;
using Amazon.S3.Model;
using Sim.System.Application.Contracts.FileStorage;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileStorage;

internal static class FileStorageConnectionTester
{
    public static async Task<FileStorageConnectionTestResultDto> TestLocalAsync(
        string? rootPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath.Trim()))
        {
            return Failed("Local storage root path must be an absolute path.");
        }

        var resolvedRoot = Path.GetFullPath(rootPath.Trim());
        Directory.CreateDirectory(resolvedRoot);
        var probePath = Path.Combine(resolvedRoot, $".sim-storage-test-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(probePath, "sim-storage-connection-test", cancellationToken);
            return Succeeded();
        }
        finally
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }
    }

    public static async Task<FileStorageConnectionTestResultDto> TestDatabaseAsync(
        ISqlSugarRepository<DatabaseStoredFileEntity> repository,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await repository.GetQueryable().AnyAsync();
        return Succeeded();
    }

    public static async Task<FileStorageConnectionTestResultDto> TestObjectStorageAsync(
        string provider,
        S3CompatibleStorageOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Region) ||
            string.IsNullOrWhiteSpace(options.Bucket) ||
            string.IsNullOrWhiteSpace(options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(options.AccessKeySecret))
        {
            return Failed("Region, bucket, Access Key ID and Access Key Secret are required.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint) &&
            (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
             endpoint.Scheme is not ("http" or "https")))
        {
            return Failed("Endpoint must be an absolute HTTP or HTTPS URL.");
        }

        var normalized = ObjectStorageProviderOptions.Normalize(provider, options);
        using var client = S3CompatibleFileStorageProvider.CreateClient(normalized);
        var response = await client.ListObjectsAsync(
            new ListObjectsRequest
            {
                BucketName = normalized.Bucket,
                MaxKeys = 1
            },
            cancellationToken);
        return response.HttpStatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices
            ? Succeeded()
            : Failed($"Storage service returned HTTP {(int)response.HttpStatusCode}.");
    }

    private static FileStorageConnectionTestResultDto Succeeded() => new() { Success = true };

    private static FileStorageConnectionTestResultDto Failed(string message) =>
        new() { Success = false, Message = message };
}
