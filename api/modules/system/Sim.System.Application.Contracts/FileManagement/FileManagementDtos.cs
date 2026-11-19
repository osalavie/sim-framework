using System.ComponentModel.DataAnnotations;

namespace Sim.System.FileManagement;

public sealed class StoredFileDto
{
    public string Provider { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Length { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class ContentFileDto
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Length { get; set; }

    /// <summary>
    /// Provider-independent application URL persisted in business content.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

public sealed class FileAccessUrlDto
{
    public string Provider { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsExternalUrl { get; set; }
}

public sealed class ResourceShareLinkDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Raw capability token. It is returned only when the link is created.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int? MaxViews { get; set; }
}

public sealed class ResourceAccessAuditDto
{
    public Guid Id { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Decision { get; set; } = string.Empty;

    public string AccessReason { get; set; } = string.Empty;

    public Guid? ShareLinkId { get; set; }

    public string? VisitorFingerprintHash { get; set; }

    public Guid? FileId { get; set; }

    public DateTime AccessedAt { get; set; }

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }
}

public sealed class StorageQuotaDto
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public long UsedBytes { get; set; }

    public long QuotaBytes { get; set; }

    public int FileCount { get; set; }

    public double UsagePercentage => QuotaBytes > 0 ? Math.Round((double)UsedBytes / QuotaBytes, 2) : 0;
}

public sealed class DirectUploadUrlDto
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string UploadUrl { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public Dictionary<string, string> RequiredHeaders { get; set; } = [];
}

public sealed class TabularExportInput
{
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = "export";

    [MinLength(1)]
    public List<string> Columns { get; set; } = [];

    [Required]
    public List<Dictionary<string, string?>> Rows { get; set; } = [];

    public TabularExportFileFormat Format { get; set; } = TabularExportFileFormat.Csv;
}

public sealed class TemplateRenderInput
{
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = "export.txt";

    [Required]
    public string Template { get; set; } = string.Empty;

    [Required]
    public string ModelJson { get; set; } = "{}";

    [StringLength(128)]
    public string ContentType { get; set; } = "text/plain; charset=utf-8";

    [MinLength(1)]
    public List<string> Columns { get; set; } = [];

    [Required]
    public List<Dictionary<string, string?>> Rows { get; set; } = [];

    public TabularExportFileFormat Format { get; set; } = TabularExportFileFormat.Csv;
}

public enum TabularExportFileFormat
{
    Csv,
    Json,
    Html
}
