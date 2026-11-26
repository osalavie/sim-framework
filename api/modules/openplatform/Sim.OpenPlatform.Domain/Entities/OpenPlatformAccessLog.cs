using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.OpenPlatform.Domain.Entities;

/// <summary>
/// 开放平台访问日志 — 记录每次令牌签发结果，便于审计第三方接入。
/// </summary>
[SugarTable("openplatform_access_log")]
public class OpenPlatformAccessLog : CreationAuditedEntity
{
    /// <summary>应用 Id</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>应用唯一标识（冗余，便于检索）</summary>
    [SugarColumn(Length = 40)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>是否成功</summary>
    public bool Succeeded { get; set; }

    /// <summary>失败原因（成功时为空）</summary>
    public string? Reason { get; set; }

    /// <summary>客户端 IP</summary>
    public string? ClientIp { get; set; }
}
