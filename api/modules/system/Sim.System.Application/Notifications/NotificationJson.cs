using System.Text.Json;

namespace Sim.System.Application.Notifications;

internal static class NotificationJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static Dictionary<string, string> DeserializeDictionary(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(value, Options)
              ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static List<string> DeserializeList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<List<string>>(value, Options) ?? [];
}
