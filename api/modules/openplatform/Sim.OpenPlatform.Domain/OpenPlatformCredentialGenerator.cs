using System.Security.Cryptography;
using System.Text;

namespace Sim.OpenPlatform.Domain;

/// <summary>
/// 开放平台接入凭证生成器。
/// AppId 与 AppSecret 均为高熵随机值；AppSecret 只在创建/重置时以明文返回一次。
/// </summary>
public static class OpenPlatformCredentialGenerator
{
    private const string AppIdPrefix = "op_";
    private const string SecretPrefix = "ops_";

    public static string GenerateAppId()
    {
        return AppIdPrefix + RandomToken(12);
    }

    public static string GenerateSecret()
    {
        return SecretPrefix + RandomToken(32);
    }

    private static string RandomToken(int byteCount)
    {
        var bytes = new byte[byteCount];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}