using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.FileStorage;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.FileManagement;
using Sim.System.Domain.FileStorage;

namespace Sim.System.Application.FileStorage;

[Authorize]
[Permission(SystemManagementPermissions.FileStorageManage)]
public class FileStorageConfigurationService(
    ISqlSugarRepository<FileStorageSettingsEntity> settingsRepository,
    ISqlSugarRepository<FileStorageProviderConfigurationEntity> providerRepository,
    ISqlSugarRepository<DatabaseStoredFileEntity> databaseFileRepository,
    IFileManagementOptionsProvider optionsProvider,
    FileStorageConfigurationProtection protection,
    FileStorageConfigurationInitializer initializer,
    ILogger<FileStorageConfigurationService> logger)
    : ApplicationService, IFileStorageConfigurationService
{
    private static readonly string[] ProviderNames =
    [
        FileStorageProviderNames.Local,
        FileStorageProviderNames.Database,
        FileStorageProviderNames.S3,
        FileStorageProviderNames.TencentCos,
        FileStorageProviderNames.AliyunOss
    ];

    /// <summary>
    /// 获取文件存储配置。
    /// </summary>
    public async Task<FileStorageConfigurationDto> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await initializer.EnsureInitializedAsync();
        var options = await optionsProvider.GetAsync(cancellationToken);
        var entities = (await providerRepository.GetAll())
            .ToDictionary(item => item.Provider, StringComparer.OrdinalIgnoreCase);
        return Map(options, entities);
    }

    /// <summary>
    /// 更新文件存储配置。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<FileStorageConfigurationDto> UpdateAsync(
        UpdateFileStorageConfigurationDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await initializer.EnsureInitializedAsync();
        Validate(input);
        var settings = (await settingsRepository.GetAll()).FirstOrDefault();
        if (settings == null)
        {
            settings = new FileStorageSettingsEntity(Guid.NewGuid());
            await settingsRepository.InsertAsync(settings);
        }

        settings.DefaultProvider = input.DefaultProvider.Trim().ToLowerInvariant();
        settings.MaxUploadBytes = input.MaxUploadBytes;
        await settingsRepository.UpdateAsync(settings);

        var existing = (await providerRepository.GetAll())
            .ToDictionary(item => item.Provider, StringComparer.OrdinalIgnoreCase);
        ValidateSecrets(input, existing);
        foreach (var providerInput in input.Providers)
        {
            var name = providerInput.Provider.Trim().ToLowerInvariant();
            var entity = existing.GetValueOrDefault(name);
            var isNew = entity == null;
            entity ??= new FileStorageProviderConfigurationEntity(Guid.NewGuid())
            {
                Provider = name
            };
            Apply(providerInput, entity);
            if (isNew)
            {
                await providerRepository.InsertAsync(entity);
            }
            else
            {
                await providerRepository.UpdateAsync(entity);
            }
        }

        logger.LogInformation(
            "File storage configuration updated. DefaultProvider={DefaultProvider}, EnabledProviders={EnabledProviders}",
            settings.DefaultProvider,
            string.Join(',', input.Providers.Where(item => item.Enabled).Select(item => item.Provider)));

        return await GetAsync(cancellationToken);
    }

    /// <summary>
    /// 测试指定文件存储提供方的连接。
    /// </summary>
    public async Task<FileStorageConnectionTestResultDto> TestAsync(
        TestFileStorageProviderConnectionDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = input.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ProviderNames.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            return new FileStorageConnectionTestResultDto
            {
                Success = false,
                Message = "Unsupported storage provider."
            };
        }

        try
        {
            if (provider == FileStorageProviderNames.Local)
            {
                return await FileStorageConnectionTester.TestLocalAsync(
                    input.RootPath,
                    cancellationToken);
            }

            if (provider == FileStorageProviderNames.Database)
            {
                return await FileStorageConnectionTester.TestDatabaseAsync(
                    databaseFileRepository,
                    cancellationToken);
            }

            var stored = (await providerRepository.GetAll()).FirstOrDefault(item =>
                string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase));
            var options = new S3CompatibleStorageOptions
            {
                Enabled = true,
                Endpoint = Normalize(input.Endpoint) ?? string.Empty,
                Region = Normalize(input.Region) ?? string.Empty,
                Bucket = Normalize(input.Bucket) ?? string.Empty,
                AccessKeyId = Normalize(input.AccessKeyId) ?? string.Empty,
                AccessKeySecret = string.IsNullOrWhiteSpace(input.AccessKeySecret)
                    ? protection.Unprotect(stored?.ProtectedAccessKeySecret) ?? string.Empty
                    : input.AccessKeySecret.Trim(),
                SessionToken = string.IsNullOrWhiteSpace(input.SessionToken)
                    ? protection.Unprotect(stored?.ProtectedSessionToken) ?? string.Empty
                    : input.SessionToken.Trim(),
                ForcePathStyle = input.ForcePathStyle
            };
            return await FileStorageConnectionTester.TestObjectStorageAsync(
                provider,
                options,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "File storage connection test failed. Provider={Provider}",
                provider);
            return new FileStorageConnectionTestResultDto
            {
                Success = false,
                Message = exception.Message
            };
        }
    }

    private void Apply(
        UpdateFileStorageProviderConfigurationDto input,
        FileStorageProviderConfigurationEntity entity)
    {
        entity.Enabled = input.Enabled;
        entity.RootPath = Normalize(input.RootPath);
        entity.Endpoint = Normalize(input.Endpoint);
        entity.Region = Normalize(input.Region);
        entity.Bucket = Normalize(input.Bucket);
        entity.AccessKeyId = Normalize(input.AccessKeyId);
        entity.ForcePathStyle = input.ForcePathStyle;
        if (input.ClearAccessKeySecret)
        {
            entity.ProtectedAccessKeySecret = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.AccessKeySecret))
        {
            entity.ProtectedAccessKeySecret = protection.Protect(input.AccessKeySecret);
        }

        if (input.ClearSessionToken)
        {
            entity.ProtectedSessionToken = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.SessionToken))
        {
            entity.ProtectedSessionToken = protection.Protect(input.SessionToken);
        }
    }

    private static void Validate(UpdateFileStorageConfigurationDto input)
    {
        var normalized = input.Providers
            .Select(item => item.Provider.Trim().ToLowerInvariant())
            .ToArray();
        if (normalized.Length != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            normalized.Any(name => !ProviderNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
        }

        var defaultName = input.DefaultProvider.Trim().ToLowerInvariant();
        var defaultProvider = input.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, defaultName, StringComparison.OrdinalIgnoreCase));
        if (defaultProvider?.Enabled != true)
        {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
        }

        foreach (var provider in input.Providers.Where(item => item.Enabled))
        {
            var name = provider.Provider.Trim().ToLowerInvariant();
            if (name == FileStorageProviderNames.Local &&
                (string.IsNullOrWhiteSpace(provider.RootPath) ||
                 !Path.IsPathFullyQualified(provider.RootPath)))
            {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
            }

            if (name is FileStorageProviderNames.S3 or
                FileStorageProviderNames.TencentCos or
                FileStorageProviderNames.AliyunOss)
            {
                if (string.IsNullOrWhiteSpace(provider.Region) ||
                    string.IsNullOrWhiteSpace(provider.Bucket) ||
                    string.IsNullOrWhiteSpace(provider.AccessKeyId))
                {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
                }

                if (!string.IsNullOrWhiteSpace(provider.Endpoint) &&
                    (!Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out var endpoint) ||
                     endpoint.Scheme is not ("http" or "https")))
                {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
                }
            }
        }
    }

    private static void ValidateSecrets(
        UpdateFileStorageConfigurationDto input,
        IReadOnlyDictionary<string, FileStorageProviderConfigurationEntity> existing)
    {
        foreach (var provider in input.Providers.Where(item => item.Enabled))
        {
            var name = provider.Provider.Trim().ToLowerInvariant();
            if (name is not (FileStorageProviderNames.S3 or
                FileStorageProviderNames.TencentCos or
                FileStorageProviderNames.AliyunOss))
            {
                continue;
            }

            var hasExistingSecret = existing.GetValueOrDefault(name)?.ProtectedAccessKeySecret != null;
            if (provider.ClearAccessKeySecret ||
                (!hasExistingSecret && string.IsNullOrWhiteSpace(provider.AccessKeySecret)))
            {
            throw new BusinessException(SystemErrorCodes.InvalidFileStorageConfiguration);
            }
        }
    }

    private static FileStorageConfigurationDto Map(
        FileManagementOptions options,
        IReadOnlyDictionary<string, FileStorageProviderConfigurationEntity> entities) => new()
    {
        DefaultProvider = options.DefaultStorageProvider,
        MaxUploadBytes = options.MaxUploadBytes,
        Providers =
        [
            MapLocal(options.Local, entities.GetValueOrDefault(FileStorageProviderNames.Local)),
            MapDatabase(options.Database, entities.GetValueOrDefault(FileStorageProviderNames.Database)),
            MapObject(FileStorageProviderNames.S3, options.S3, entities),
            MapObject(FileStorageProviderNames.TencentCos, options.TencentCos, entities),
            MapObject(FileStorageProviderNames.AliyunOss, options.AliyunOss, entities)
        ]
    };

    private static FileStorageProviderConfigurationDto MapLocal(
        LocalFileStorageOptions options,
        FileStorageProviderConfigurationEntity? entity) => new()
    {
        Provider = FileStorageProviderNames.Local,
        Enabled = options.Enabled,
        RootPath = options.RootPath,
        HasAccessKeySecret = entity?.ProtectedAccessKeySecret != null,
        HasSessionToken = entity?.ProtectedSessionToken != null
    };

    private static FileStorageProviderConfigurationDto MapDatabase(
        DatabaseFileStorageOptions options,
        FileStorageProviderConfigurationEntity? entity) => new()
    {
        Provider = FileStorageProviderNames.Database,
        Enabled = options.Enabled,
        HasAccessKeySecret = entity?.ProtectedAccessKeySecret != null,
        HasSessionToken = entity?.ProtectedSessionToken != null
    };

    private static FileStorageProviderConfigurationDto MapObject(
        string name,
        S3CompatibleStorageOptions options,
        IReadOnlyDictionary<string, FileStorageProviderConfigurationEntity> entities)
    {
        var entity = entities.GetValueOrDefault(name);
        return new FileStorageProviderConfigurationDto
        {
            Provider = name,
            Enabled = options.Enabled,
            Endpoint = options.Endpoint,
            Region = options.Region,
            Bucket = options.Bucket,
            AccessKeyId = options.AccessKeyId,
            HasAccessKeySecret = entity?.ProtectedAccessKeySecret != null,
            HasSessionToken = entity?.ProtectedSessionToken != null,
            ForcePathStyle = options.ForcePathStyle
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
