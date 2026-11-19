namespace Sim.System.FileManagement;

public sealed record StoredAvatar(
    string Url,
    string Provider,
    string Key,
    string ContentType,
    long Length);

public interface IAvatarStorage
{
    Task<StoredAvatar> SaveAsync(
        string scope,
        Guid ownerId,
        Stream content,
        CancellationToken cancellationToken = default);
}

public sealed class InvalidAvatarFileException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class AvatarStorageKey
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static bool IsPublicAvatar(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512)
        {
            return false;
        }

        var normalized = key.Replace('\\', '/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.StartsWith("avatars/users/", StringComparison.Ordinal) &&
               (normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
    }
}
