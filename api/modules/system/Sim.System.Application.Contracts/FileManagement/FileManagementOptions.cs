using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.FileManagement;

public sealed class FileManagementOptions
{
    public const string SectionName = "FileManagement";

    public string DefaultStorageProvider { get; set; } = FileStorageProviderNames.Local;

    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    public string TemporaryPath { get; set; } = "App_Data/file-processing";

    /// <summary>
    /// Base URL of this server, used to construct access URLs for Local/Database providers.
    /// Example: "https://api.example.com"
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret key for HMAC-SHA256 signing of Local/Database file access URLs.
    /// Must be a cryptographically random string (>= 32 chars). Rotate on schedule.
    /// </summary>
    public string AccessUrlSigningKey { get; set; } = string.Empty;

    public LocalFileStorageOptions Local { get; set; } = new();

    public DatabaseFileStorageOptions Database { get; set; } = new();

    public ExternalToolOptions LibreOffice { get; set; } = new() { ExecutablePath = "soffice" };

    public ExternalToolOptions ImageMagick { get; set; } = new() { ExecutablePath = "magick" };

    public S3CompatibleStorageOptions S3 { get; set; } = new();

    public S3CompatibleStorageOptions TencentCos { get; set; } = new();

    public S3CompatibleStorageOptions AliyunOss { get; set; } = new();
}

public sealed class LocalFileStorageOptions
{
    public bool Enabled { get; set; } = true;

    public string RootPath { get; set; } = "App_Data/files";
}

public sealed class DatabaseFileStorageOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class ExternalToolOptions
{
    public bool Enabled { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class S3CompatibleStorageOptions
{
    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string AccessKeySecret { get; set; } = string.Empty;

    public string SessionToken { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; }
}
