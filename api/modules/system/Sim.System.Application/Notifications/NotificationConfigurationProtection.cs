using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Sim.System.Application.Notifications;

public sealed class NotificationConfigurationProtection(IDataProtectionProvider provider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector = provider.CreateProtector(
        "SimFramework.System.NotificationConfiguration.v1");

    public string? Protect(IReadOnlyDictionary<string, string>? values) =>
        values is not { Count: > 0 }
            ? null
            : _protector.Protect(JsonSerializer.Serialize(values, SerializerOptions));

    public Dictionary<string, string> Unprotect(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(
                  _protector.Unprotect(value),
                  SerializerOptions)
              ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
