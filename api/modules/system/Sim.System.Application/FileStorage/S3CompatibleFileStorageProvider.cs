using System.Net;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Sim.System.FileManagement;

namespace Sim.System.Application.FileStorage;

public abstract class S3CompatibleFileStorageProvider : IFileStorageProvider
{
    private readonly IFileManagementOptionsProvider _optionsProvider;
    private readonly Func<FileManagementOptions, S3CompatibleStorageOptions> _optionsSelector;

    protected S3CompatibleFileStorageProvider(
        string name,
        IFileManagementOptionsProvider optionsProvider,
        Func<FileManagementOptions, S3CompatibleStorageOptions> optionsSelector)
    {
        Name = name;
        _optionsProvider = optionsProvider;
        _optionsSelector = optionsSelector;
    }

    public string Name { get; }

    public async Task<FileStorageWriteResult> SaveAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);
        var putRequest = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = request.Key,
            InputStream = request.Content,
            ContentType = request.ContentType,
            AutoCloseStream = false
        };
        putRequest.Metadata["file-name-base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.FileName));
        putRequest.Metadata["sha256"] = request.Sha256;
        var response = await client.PutObjectAsync(putRequest, cancellationToken);
        if (response.HttpStatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw new InvalidOperationException($"{Name} returned HTTP {(int)response.HttpStatusCode} while saving an object.");
        }

        return new FileStorageWriteResult(
            Name,
            request.Key,
            request.FileName,
            request.ContentType,
            request.Length,
            request.Sha256);
    }

    public async Task<FileStorageReadResult?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);
        try
        {
            using var response = await client.GetObjectAsync(options.Bucket, key, cancellationToken);
            var content = new MemoryStream();
            await response.ResponseStream.CopyToAsync(content, cancellationToken);
            content.Position = 0;
            var encodedFileName = TryGetMetadata(response.Metadata, "file-name-base64");
            var fileName = string.IsNullOrWhiteSpace(encodedFileName)
                ? Path.GetFileName(key)
                : Encoding.UTF8.GetString(Convert.FromBase64String(encodedFileName));
            return new FileStorageReadResult(
                Name,
                key,
                fileName,
                response.Headers.ContentType ?? "application/octet-stream",
                content.Length,
                content);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);
        try
        {
            await client.GetObjectMetadataAsync(options.Bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);

        // Verify the object exists before generating a URL
        if (!await ExistsInternalAsync(client, options.Bucket, key, cancellationToken))
        {
            return null;
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.GET
        };

        var url = client.GetPreSignedURL(request);
        return new FileAccessUrlResult(
            Name,
            key,
            url,
            new DateTimeOffset(request.Expires!.Value, TimeSpan.Zero),
            IsExternalUrl: true);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);
        await client.DeleteObjectAsync(options.Bucket, key, cancellationToken);
    }

    public async Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
        string key,
        string fileName,
        string contentType,
        long contentLength,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var options = await GetOptionsAsync(cancellationToken);
        using var client = CreateClient(options);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.PUT,
            ContentType = contentType
        };

        // Embed metadata for the server to read after the direct upload completes
        request.Metadata["file-name-base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileName));

        var url = client.GetPreSignedURL(request);
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = contentType
        };

        return new FileUploadUrlResult(
            Name,
            key,
            url,
            new DateTimeOffset(request.Expires!.Value, TimeSpan.Zero),
            headers);
    }

    private async Task<S3CompatibleStorageOptions> GetOptionsAsync(
        CancellationToken cancellationToken)
    {
        var runtimeOptions = await _optionsProvider.GetAsync(cancellationToken);
        var options = _optionsSelector(runtimeOptions);
        ValidateConfiguration(options);
        return options;
    }

    internal static IAmazonS3 CreateClient(S3CompatibleStorageOptions options)
    {
        AWSCredentials credentials = string.IsNullOrWhiteSpace(options.SessionToken)
            ? new BasicAWSCredentials(options.AccessKeyId, options.AccessKeySecret)
            : new SessionAWSCredentials(
                options.AccessKeyId,
                options.AccessKeySecret,
                options.SessionToken);
        var config = new AmazonS3Config
        {
            AuthenticationRegion = options.Region,
            ForcePathStyle = options.ForcePathStyle,
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = 3,
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.Endpoint.TrimEnd('/');
        }

        return new AmazonS3Client(credentials, config);
    }

    private void ValidateConfiguration(S3CompatibleStorageOptions options)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException($"File storage provider '{Name}' is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Region) ||
            string.IsNullOrWhiteSpace(options.Bucket) ||
            string.IsNullOrWhiteSpace(options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(options.AccessKeySecret))
        {
            throw new InvalidOperationException(
                $"File storage provider '{Name}' requires region, bucket and credentials.");
        }
    }

    private static string? TryGetMetadata(MetadataCollection metadata, string key)
    {
        var direct = metadata[key];
        return string.IsNullOrWhiteSpace(direct) ? metadata[$"x-amz-meta-{key}"] : direct;
    }

    private static async Task<bool> ExistsInternalAsync(
        IAmazonS3 client,
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.GetObjectMetadataAsync(bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
