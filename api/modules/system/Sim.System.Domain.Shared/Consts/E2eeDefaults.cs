namespace Sim.System.Domain.Shared.Consts;

/// <summary>
/// 私有文章端到端加密（E2EE）身份参数。服务端只保存包裹后的密钥与 KDF 参数，
/// 永远不接收、不派生、不存储明文 UMK/RK 或加密口令。
/// </summary>
public static class E2eeDefaults
{
    /// <summary>v1 固定使用 PBKDF2-HMAC-SM3，满足国密摘要要求。</summary>
    public const string KdfAlgorithm = "PBKDF2-SM3";

    /// <summary>与 Web Crypto 版本保持同量级的迭代成本；参数随身份行保存以便后续迁移。</summary>
    public const int KdfIterations = 600_000;

    /// <summary>16 字节随机 salt 的 base64url。</summary>
    public const int KdfSaltLength = 128;

    /// <summary>每个用户同一时刻只有一条 active 身份行。</summary>
    public const int InitialKeyVersion = 1;
}
