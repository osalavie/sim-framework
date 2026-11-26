namespace Sim.System.Application.Notifications;

internal static class NotificationMask
{
    public static string Mask(string value)
    {
        if (value.Length <= 4) return "****";
        if (value.Contains('@'))
        {
            var parts = value.Split('@', 2);
            return $"{parts[0][0]}***@{parts[1]}";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}/***";
        }

        return $"{value[..2]}***{value[^2..]}";
    }
}
