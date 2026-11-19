namespace Sim.OpenPlatform.Application.Contracts.Dtos;

/// <summary>
/// 第三方应用 DTO（管理端查看与列表展示）。
/// </summary>
public class OpenPlatformApplicationDto
{
    /// <summary>应用唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>应用名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>应用 AppId（对外唯一标识）。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>应用描述。</summary>
    public string? Description { get; set; }

    /// <summary>是否启用：仅启用状态可换取 Access Token。</summary>
    public bool IsActive { get; set; }

    /// <summary>已授予的权限范围（权限码列表）。</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>允许调用令牌端点的客户端 IP 列表（为空表示不限制）。</summary>
    public List<string> AllowedIps { get; set; } = new();

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>创建人标识。</summary>
    public Guid? CreatorId { get; set; }

    /// <summary>最后修改时间。</summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>最后修改人标识。</summary>
    public Guid? LastModifierId { get; set; }
}

/// <summary>
/// 创建第三方应用的请求参数。
/// </summary>
public class CreateOpenPlatformApplicationDto
{
    /// <summary>应用名称（必填）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>应用描述。</summary>
    public string? Description { get; set; }

    /// <summary>授予的权限范围（权限码列表）。</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>允许调用令牌端点的客户端 IP 列表（为空表示不限制）。</summary>
    public List<string> AllowedIps { get; set; } = new();
}

/// <summary>
/// 创建第三方应用的结果，包含一次性返回的明文 AppSecret。
/// </summary>
public class CreateOpenPlatformApplicationResultDto
{
    /// <summary>应用唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>应用 AppId（对外唯一标识）。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>仅创建时明文返回一次，之后不再回显。</summary>
    public string AppSecret { get; set; } = string.Empty;
}

/// <summary>
/// 更新第三方应用的请求参数，仅更新传入的非空字段。
/// </summary>
public class UpdateOpenPlatformApplicationDto
{
    /// <summary>应用名称。</summary>
    public string? Name { get; set; }

    /// <summary>应用描述。</summary>
    public string? Description { get; set; }

    /// <summary>授予的权限范围（权限码列表）。</summary>
    public List<string>? Scopes { get; set; }

    /// <summary>允许调用令牌端点的客户端 IP 列表（为空表示不限制）。</summary>
    public List<string>? AllowedIps { get; set; }
}

/// <summary>
/// 重置第三方应用 AppSecret 的结果，包含一次性返回的明文 AppSecret。
/// </summary>
public class ResetOpenPlatformSecretResultDto
{
    /// <summary>仅重置时明文返回一次，之后不再回显。</summary>
    public string AppSecret { get; set; } = string.Empty;
}

/// <summary>
/// 开放平台访问日志 DTO（令牌签发审计记录）。
/// </summary>
public class OpenPlatformAccessLogDto
{
    /// <summary>日志记录唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>发起请求的第三方应用 ID。</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>发起请求的第三方应用 AppId。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>签发结果：true 成功，false 失败。</summary>
    public bool Succeeded { get; set; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? Reason { get; set; }

    /// <summary>发起请求的客户端 IP。</summary>
    public string? ClientIp { get; set; }

    /// <summary>请求时间。</summary>
    public DateTime CreationTime { get; set; }
}