namespace Sim.System.Application.E2ee;

/// <summary>
/// E2EE wrap:v1 信封与 KDF 参数的形状校验。服务端只检查格式，不持有任何明文密钥。
/// </summary>
internal static class E2eeWrapFormat
{
    private const string WrapPrefix = "wrap:v1:";
    private const int WrappedKeyBytes = 44; // nonce[12] + tag[16] + SM4 key[16]
    private const int SaltBytes = 16;

    public static bool IsWrappedKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(WrapPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return TryDecodeBase64Url(value[WrapPrefix.Length..], out var payload) &&
               payload.Length == WrappedKeyBytes;
    }

    public static bool IsValidSalt(string? value) =>
        TryDecodeBase64Url(value, out var salt) && salt.Length == SaltBytes;

    public static bool IsValidAccountBackup(
        string? wrappedRecoveryKey,
        string? salt,
        int? iterations) =>
        IsWrappedKey(wrappedRecoveryKey) &&
        IsValidSalt(salt) &&
        iterations is >= 10_000 and <= 10_000_000;

    public static bool TryDecodeBase64Url(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            var valid = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
            if (!valid)
            {
                return false;
            }
        }

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - normalized.Length % 4) % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + padding, '=');
        }

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
