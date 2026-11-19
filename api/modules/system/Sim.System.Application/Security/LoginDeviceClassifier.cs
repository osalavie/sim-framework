using System.Security.Cryptography;
using System.Text;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Security;

public sealed record LoginDeviceDescriptor(
    string IdentifierHash,
    string Type,
    string Name);

public sealed record ActiveLoginSession(
    Guid Id,
    string? DeviceIdentifierHash,
    string? DeviceType,
    DateTime LastSeenAt);

public static class LoginSessionLimitPolicy
{
    public static IReadOnlyList<Guid> SelectSessionsToRevoke(
        IReadOnlyCollection<ActiveLoginSession> sessions,
        LoginDeviceDescriptor device,
        int limit)
    {
        var sameDeviceSessionIds = sessions
            .Where(session => session.DeviceIdentifierHash == device.IdentifierHash)
            .Select(session => session.Id)
            .ToHashSet();
        var remainingTypeSessions = sessions
            .Where(session =>
                !sameDeviceSessionIds.Contains(session.Id) &&
                string.Equals(session.DeviceType, device.Type, StringComparison.Ordinal))
            .OrderBy(session => session.LastSeenAt)
            .ToList();
        var excessCount = Math.Max(0, remainingTypeSessions.Count - limit + 1);

        return sameDeviceSessionIds
            .Concat(remainingTypeSessions.Take(excessCount).Select(session => session.Id))
            .Distinct()
            .ToList();
    }
}

public static class LoginDeviceClassifier
{
    public static LoginDeviceDescriptor Classify(
        string? deviceId,
        string? deviceType,
        string? deviceName,
        string? userAgent,
        string? clientIp,
        string? existingIdentifierHash = null)
    {
        var normalizedType = NormalizeType(deviceType, userAgent);
        var normalizedName = NormalizeName(deviceName, normalizedType, userAgent);
        var identifierHash = !string.IsNullOrWhiteSpace(deviceId)
            ? Hash($"client:{deviceId.Trim()}")
            : !string.IsNullOrWhiteSpace(existingIdentifierHash)
                ? existingIdentifierHash
                : Hash($"fallback:{userAgent?.Trim()}|{clientIp?.Trim()}");

        return new LoginDeviceDescriptor(identifierHash, normalizedType, normalizedName);
    }

    private static string NormalizeType(string? deviceType, string? userAgent)
    {
        var explicitType = LoginDeviceTypes.All.FirstOrDefault(type =>
            string.Equals(type, deviceType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (explicitType != null)
        {
            return explicitType;
        }

        var value = userAgent ?? string.Empty;
        if (value.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Pad", StringComparison.OrdinalIgnoreCase) ||
            (value.Contains("Android", StringComparison.OrdinalIgnoreCase) &&
             !value.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
        {
            return LoginDeviceTypes.Pad;
        }

        if (value.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            return LoginDeviceTypes.Phone;
        }

        return LoginDeviceTypes.Pc;
    }

    private static string NormalizeName(string? deviceName, string deviceType, string? userAgent)
    {
        var value = string.IsNullOrWhiteSpace(deviceName)
            ? BuildFallbackName(deviceType, userAgent)
            : deviceName.Trim();
        return value.Length <= 128 ? value : value[..128];
    }

    private static string BuildFallbackName(string deviceType, string? userAgent)
    {
        var browser = userAgent switch
        {
            { } value when value.Contains("Edg/", StringComparison.OrdinalIgnoreCase) => "Edge",
            { } value when value.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) => "Firefox",
            { } value when value.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) => "Chrome",
            { } value when value.Contains("Safari/", StringComparison.OrdinalIgnoreCase) => "Safari",
            _ => "Unknown client"
        };
        return $"{deviceType} · {browser}";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
