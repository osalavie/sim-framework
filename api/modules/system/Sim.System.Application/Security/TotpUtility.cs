using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Sim.System.Application.Security;

public static class TotpUtility
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] GenerateSecret(int byteLength = 20) =>
        RandomNumberGenerator.GetBytes(byteLength);

    public static bool VerifyCode(
        byte[] secret,
        string? code,
        DateTime utcNow,
        int allowedTimeStepDrift = 1)
    {
        var normalized = NormalizeCode(code);
        if (normalized == null)
        {
            return false;
        }

        var currentStep = new DateTimeOffset(utcNow).ToUnixTimeSeconds() / 30;
        for (var drift = -allowedTimeStepDrift; drift <= allowedTimeStepDrift; drift++)
        {
            var expected = ComputeCode(secret, currentStep + drift);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    public static string ComputeCode(byte[] secret, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);
        var hash = HMACSHA1.HashData(secret, counter);
        var offset = hash[^1] & 0x0f;
        var binaryCode =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);
        return (binaryCode % 1_000_000).ToString("D6");
    }

    public static string ToBase32(byte[] value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var output = new StringBuilder((value.Length * 8 + 4) / 5);
        var buffer = (int)value[0];
        var next = 1;
        var bitsLeft = 8;
        while (bitsLeft > 0 || next < value.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < value.Length)
                {
                    buffer <<= 8;
                    buffer |= value[next++] & 0xff;
                    bitsLeft += 8;
                }
                else
                {
                    buffer <<= 5 - bitsLeft;
                    bitsLeft = 5;
                }
            }

            output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 0x1f]);
            bitsLeft -= 5;
        }

        return output.ToString();
    }

    public static byte[] FromBase32(string value)
    {
        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=')
            .ToUpperInvariant();
        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in normalized)
        {
            var index = Base32Alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new FormatException("Invalid Base32 value.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft < 8)
            {
                continue;
            }

            output.Add((byte)(buffer >> (bitsLeft - 8)));
            bitsLeft -= 8;
        }

        return output.ToArray();
    }

    private static string? NormalizeCode(string? code)
    {
        var normalized = code?.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized is { Length: 6 } && normalized.All(char.IsAsciiDigit)
            ? normalized
            : null;
    }
}
