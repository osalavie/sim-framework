using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.OpenPlatform.Domain.Entities;

/// <summary>
/// 开放平台第三方应用 — 机器对机器调用本系统 API 的接入凭证。
/// AppId 对外公开；AppSecret 使用 Data Protection 加密保存，仅在创建/重置时明文返回一次。
/// </summary>
[SugarTable("openplatform_application")]
[SugarIndex("idx_openplatform_application_app_id", nameof(AppId), OrderByType.Asc, true)]
public class OpenPlatformApplication : AuditedAggregateRoot
{
    /// <summary>应用名称</summary>
    [SugarColumn(Length = 120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>应用唯一标识（对外公开）</summary>
    [SugarColumn(Length = 40)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>应用密钥（Data Protection 加密存储，不回显）</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string ProtectedAppSecret { get; set; } = string.Empty;

    /// <summary>应用说明</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>授权范围（权限码 JSON 数组；签发 Token 时逐个写入 Permission Claim）</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Scopes { get; set; }

    /// <summary>允许的来源 IP 白名单（JSON 数组；为空表示不限制）</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? AllowedIps { get; set; }
}
