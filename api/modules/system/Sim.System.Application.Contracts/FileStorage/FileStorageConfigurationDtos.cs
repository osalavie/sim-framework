using System.ComponentModel.DataAnnotations;

namespace Sim.System.Application.Contracts.FileStorage;

public sealed class FileStorageConfigurationDto
{
    public string DefaultProvider { get; set; } = "local";
    public long MaxUploadBytes { get; set; }
    public List<FileStorageProviderConfigurationDto> Providers { get; set; } = [];
}

public sealed class FileStorageProviderConfigurationDto
{
    public string Provider { get; set; } = string.Empty;
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

public sealed class UpdateFileStorageConfigurationDto
{
    [Required]
    [MaxLength(64)]
    public string DefaultProvider { get; set; } = "local";

    [Range(1024 * 1024, 5L * 1024 * 1024 * 1024)]
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    [MinLength(1)]
    public List<UpdateFileStorageProviderConfigurationDto> Providers { get; set; } = [];
}

public sealed class UpdateFileStorageProviderConfigurationDto
{
    [Required]
    [MaxLength(64)]
    public string Provider { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [MaxLength(1024)]
    public string? RootPath { get; set; }

    [MaxLength(1024)]
    public string? Endpoint { get; set; }

    [MaxLength(128)]
    public string? Region { get; set; }

    [MaxLength(256)]
    public string? Bucket { get; set; }

    [MaxLength(512)]
    public string? AccessKeyId { get; set; }

    [MaxLength(2048)]
    public string? AccessKeySecret { get; set; }

    public bool ClearAccessKeySecret { get; set; }

    [MaxLength(4096)]
    public string? SessionToken { get; set; }

    public bool ClearSessionToken { get; set; }

    public bool ForcePathStyle { get; set; }
}

public sealed class TestFileStorageProviderConnectionDto
{
    [Required]
    [MaxLength(64)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? RootPath { get; set; }

    [MaxLength(1024)]
    public string? Endpoint { get; set; }

    [MaxLength(128)]
    public string? Region { get; set; }

    [MaxLength(256)]
    public string? Bucket { get; set; }

    [MaxLength(512)]
    public string? AccessKeyId { get; set; }

    [MaxLength(2048)]
    public string? AccessKeySecret { get; set; }

    [MaxLength(4096)]
    public string? SessionToken { get; set; }

    public bool ForcePathStyle { get; set; }
}

public sealed class FileStorageConnectionTestResultDto
{
    public bool Success { get; set; }

    public string? Message { get; set; }
}
