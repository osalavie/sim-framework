using System;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.Security;

/// <summary>
/// 刷新令牌实体
/// </summary>
[SugarTable("system_refresh_token")]
[SugarIndex("idx_refresh_token_hash", nameof(Token), OrderByType.Asc, true)]
[SugarIndex("idx_refresh_token_family", nameof(FamilyId), OrderByType.Asc)]
[SugarIndex(
    "idx_refresh_token_user_device",
    nameof(UserId),
    OrderByType.Asc,
    nameof(DeviceType),
    OrderByType.Asc)]
public class RefreshTokenEntity : CreationAuditedEntity
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public Guid UserId { get; set; }

    /// <summary>
    /// 令牌家族ID，用于在检测到重放时撤销整条令牌链。
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public Guid FamilyId { get; set; }
    
    /// <summary>
    /// 刷新令牌的 SHA-256 哈希（不保存明文）
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// 过期时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// 是否已撤销
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsRevoked { get; set; }
    
    /// <summary>
    /// 撤销时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// 替换令牌的 SHA-256 哈希（用于令牌轮换）
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ReplacedByToken { get; set; }
    
    /// <summary>
    /// 客户端IP地址
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// 用户代理
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 客户端提供的稳定设备标识的 SHA-256 摘要。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? DeviceIdentifierHash { get; set; }

    /// <summary>
    /// 归一化设备类型：PC、Pad 或 Phone。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = true)]
    public string? DeviceType { get; set; }

    /// <summary>
    /// 用于会话监控展示的设备名称，不作为安全凭据。
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// 本次登录会话开始时间；令牌轮换时保持不变。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? SessionStartedAt { get; set; }

    /// <summary>
    /// 最近一次通过会话校验或刷新令牌的时间。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LastSeenAt { get; set; }
    
    /// <summary>
    /// 是否有效（计算属性，不存储到数据库）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;
}
