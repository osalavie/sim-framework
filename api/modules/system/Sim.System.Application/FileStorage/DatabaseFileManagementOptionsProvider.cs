using Microsoft.Extensions.Options;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.FileStorage;

namespace Sim.System.Application.FileStorage;

public sealed class DatabaseFileManagementOptionsProvider(
    ISqlSugarRepository<FileStorageSettingsEntity> settingsRepository,
    ISqlSugarRepository<FileStorageProviderConfigurationEntity> providerRepository,
    IOptions<FileManagementOptions> configuredOptions,
    FileStorageConfigurationProtection protection) : IFileManagementOptionsProvider
{
    public async Task<FileManagementOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fallback = configuredOptions.Value;
        var settings = (await settingsRepository.GetAll()).FirstOrDefault();
        var providers = (await providerRepository.GetAll())
            .ToDictionary(item => item.Provider, StringComparer.OrdinalIgnoreCase);

        return new FileManagementOptions
        {
            DefaultStorageProvider = settings?.DefaultProvider ?? fallback.DefaultStorageProvider,
            MaxUploadBytes = settings?.MaxUploadBytes ?? fallback.MaxUploadBytes,
            TemporaryPath = fallback.TemporaryPath,
            LibreOffice = fallback.LibreOffice,
            ImageMagick = fallback.ImageMagick,
            Local = MapLocal(providers.GetValueOrDefault(FileStorageProviderNames.Local), fallback.Local),
            Database = MapDatabase(
                providers.GetValueOrDefault(FileStorageProviderNames.Database),
                fallback.Database),
            S3 = MapObject(providers.GetValueOrDefault(FileStorageProviderNames.S3), fallback.S3),
            TencentCos = MapObject(
                providers.GetValueOrDefault(FileStorageProviderNames.TencentCos),
                fallback.TencentCos),
            AliyunOss = MapObject(
                providers.GetValueOrDefault(FileStorageProviderNames.AliyunOss),
                fallback.AliyunOss)
        };
    }

    private static LocalFileStorageOptions MapLocal(
        FileStorageProviderConfigurationEntity? entity,
        LocalFileStorageOptions fallback) => new()
    {
        Enabled = entity?.Enabled ?? fallback.Enabled,
        RootPath = string.IsNullOrWhiteSpace(entity?.RootPath) ? fallback.RootPath : entity.RootPath
    };

    private static DatabaseFileStorageOptions MapDatabase(
        FileStorageProviderConfigurationEntity? entity,
        DatabaseFileStorageOptions fallback) => new()
    {
        Enabled = entity?.Enabled ?? fallback.Enabled
    };

    private S3CompatibleStorageOptions MapObject(
        FileStorageProviderConfigurationEntity? entity,
        S3CompatibleStorageOptions fallback) => entity == null
        ? fallback
        : new S3CompatibleStorageOptions
        {
            Enabled = entity.Enabled,
            Endpoint = entity.Endpoint ?? string.Empty,
            Region = entity.Region ?? string.Empty,
            Bucket = entity.Bucket ?? string.Empty,
            AccessKeyId = entity.AccessKeyId ?? string.Empty,
            AccessKeySecret = protection.Unprotect(entity.ProtectedAccessKeySecret) ?? string.Empty,
            SessionToken = protection.Unprotect(entity.ProtectedSessionToken) ?? string.Empty,
            ForcePathStyle = entity.ForcePathStyle
        };
}
