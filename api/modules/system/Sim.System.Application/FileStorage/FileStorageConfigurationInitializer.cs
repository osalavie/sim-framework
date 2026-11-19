using Microsoft.Extensions.Options;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.FileStorage;

namespace Sim.System.Application.FileStorage;

public sealed class FileStorageConfigurationInitializer(
    ISqlSugarRepository<FileStorageSettingsEntity> settingsRepository,
    ISqlSugarRepository<FileStorageProviderConfigurationEntity> providerRepository,
    IOptions<FileManagementOptions> configuredOptions)
{
    public async Task EnsureInitializedAsync()
    {
        if (!(await settingsRepository.GetAll()).Any())
        {
            var options = configuredOptions.Value;
            await settingsRepository.InsertAsync(new FileStorageSettingsEntity(Guid.NewGuid())
            {
                DefaultProvider = options.DefaultStorageProvider,
                MaxUploadBytes = options.MaxUploadBytes
            });
        }

        var existing = (await providerRepository.GetAll())
            .Select(item => item.Provider)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var optionsValue = configuredOptions.Value;
        var defaults = new[]
        {
            New(Guid.NewGuid(), FileStorageProviderNames.Local, optionsValue.Local.Enabled, optionsValue.Local.RootPath),
            New(Guid.NewGuid(), FileStorageProviderNames.Database, optionsValue.Database.Enabled),
            NewObject(Guid.NewGuid(), FileStorageProviderNames.S3, optionsValue.S3),
            NewObject(Guid.NewGuid(), FileStorageProviderNames.TencentCos, optionsValue.TencentCos),
            NewObject(Guid.NewGuid(), FileStorageProviderNames.AliyunOss, optionsValue.AliyunOss)
        };
        foreach (var provider in defaults.Where(item => !existing.Contains(item.Provider)))
        {
            await providerRepository.InsertAsync(provider);
        }
    }

    private static FileStorageProviderConfigurationEntity New(
        Guid id,
        string provider,
        bool enabled,
        string? rootPath = null) => new(id)
    {
        Provider = provider,
        Enabled = enabled,
        RootPath = rootPath
    };

    private static FileStorageProviderConfigurationEntity NewObject(
        Guid id,
        string provider,
        S3CompatibleStorageOptions options) => new(id)
    {
        Provider = provider,
        Enabled = options.Enabled,
        Endpoint = options.Endpoint,
        Region = options.Region,
        Bucket = options.Bucket,
        AccessKeyId = options.AccessKeyId,
        ForcePathStyle = options.ForcePathStyle
    };
}
