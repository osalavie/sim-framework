using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.OpenPlatform.Domain.Entities;

/// <summary>
/// 开放平台逐请求调用审计 — 记录携带开放平台 Access Token 的每次 API 调用。
/// </summary>
[SugarTable("openplatform_api_call_log")]
public class OpenPlatformApiCallLog : CreationAuditedEntity
{
    /// <summary>应用 Id（未知应用可为 Guid.Empty）</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>应用唯一标识（冗余，便于检索）</summary>
    [SugarColumn(Length = 40)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>请求方法</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>请求路径（不含查询串和敏感参数）</summary>
    [SugarColumn(Length = 2048)]
    public string Path { get; set; } = string.Empty;

    /// <summary>响应状态码</summary>
    public int StatusCode { get; set; }

    /// <summary>客户端 IP</summary>
    public string? ClientIp { get; set; }

    /// <summary>请求耗时（毫秒）</summary>
    public long DurationMs { get; set; }
}
