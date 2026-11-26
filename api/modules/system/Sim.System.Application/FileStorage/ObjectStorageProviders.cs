using Sim.System.FileManagement;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.FileStorage;

public sealed class S3FileStorageProvider(IFileManagementOptionsProvider optionsProvider)
    : S3CompatibleFileStorageProvider(
        FileStorageProviderNames.S3,
        optionsProvider,
        options => options.S3);

public sealed class TencentCosFileStorageProvider : S3CompatibleFileStorageProvider
{
    public TencentCosFileStorageProvider(IFileManagementOptionsProvider optionsProvider)
        : base(
            FileStorageProviderNames.TencentCos,
            optionsProvider,
            options => ObjectStorageProviderOptions.Normalize(
                FileStorageProviderNames.TencentCos,
                options.TencentCos))
    {
    }
}

public sealed class AliyunOssFileStorageProvider : S3CompatibleFileStorageProvider
{
    public AliyunOssFileStorageProvider(IFileManagementOptionsProvider optionsProvider)
        : base(
            FileStorageProviderNames.AliyunOss,
            optionsProvider,
            options => ObjectStorageProviderOptions.Normalize(
                FileStorageProviderNames.AliyunOss,
                options.AliyunOss))
    {
    }
}

internal static class ObjectStorageProviderOptions
{
    public static S3CompatibleStorageOptions Normalize(
        string provider,
        S3CompatibleStorageOptions options) => provider switch
    {
        FileStorageProviderNames.TencentCos => Copy(
            options,
            string.IsNullOrWhiteSpace(options.Endpoint) && !string.IsNullOrWhiteSpace(options.Region)
                ? $"https://cos.{options.Region}.myqcloud.com"
                : options.Endpoint),
        FileStorageProviderNames.AliyunOss => Copy(
            options,
            string.IsNullOrWhiteSpace(options.Endpoint) && !string.IsNullOrWhiteSpace(options.Region)
                ? $"https://oss-{options.Region}.aliyuncs.com"
                : options.Endpoint),
        _ => options
    };

    private static S3CompatibleStorageOptions Copy(
        S3CompatibleStorageOptions source,
        string? endpoint) =>
        new()
        {
            Enabled = source.Enabled,
            Endpoint = endpoint ?? string.Empty,
            Region = source.Region,
            Bucket = source.Bucket,
            AccessKeyId = source.AccessKeyId,
            AccessKeySecret = source.AccessKeySecret,
            SessionToken = source.SessionToken,
            ForcePathStyle = false
        };
}
