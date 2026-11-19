using System.Net.Http;

namespace Sim.Admin.Wpf.Client.Services;

public sealed partial class ApiClient
{
    public Task<StorageConfiguration> GetStorageConfigurationAsync() =>
        SendJsonAsync<StorageConfiguration>(HttpMethod.Get, "api/system/file-storage-configuration");

    public Task<StorageConfiguration> UpdateStorageConfigurationAsync(StorageConfigurationUpdate input) =>
        SendJsonAsync<StorageConfiguration>(HttpMethod.Put, "api/system/file-storage-configuration", input);

    public Task<StorageTestResult> TestStorageProviderAsync(StorageTestRequest input) =>
        SendJsonAsync<StorageTestResult>(HttpMethod.Post,
            "api/system/file-storage-configuration/test", input);
}

public sealed class StorageConfiguration
{
    public string? DefaultProvider { get; set; }
    public long MaxUploadBytes { get; set; }
    public List<StorageProviderConfiguration> Providers { get; set; } = [];
}

public sealed class StorageProviderConfiguration
{
    public string? Provider { get; set; }
    public bool Enabled { get; set; }
    public string? RootPath { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public bool HasAccessKeySecret { get; set; }
    public bool HasSessionToken { get; set; }
    public bool ForcePathStyle { get; set; }
}

public sealed class StorageConfigurationUpdate
{
    public string DefaultProvider { get; set; } = "local";
    public long MaxUploadBytes { get; set; }
    public List<StorageProviderUpdate> Providers { get; set; } = [];
}

public sealed class StorageProviderUpdate
{
    public string Provider { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? RootPath { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public string? AccessKeySecret { get; set; }
    public bool ClearAccessKeySecret { get; set; }
    public string? SessionToken { get; set; }
    public bool ClearSessionToken { get; set; }
    public bool ForcePathStyle { get; set; }
}

public sealed class StorageTestRequest
{
    public string Provider { get; set; } = string.Empty;
    public string? RootPath { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public string? AccessKeySecret { get; set; }
    public string? SessionToken { get; set; }
    public bool ForcePathStyle { get; set; }
}

public sealed class StorageTestResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
