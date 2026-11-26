using System.Security.Cryptography;

namespace Sim.System.Application.FileManagement;

public sealed record BufferedFile(Stream Content, long Length, string Sha256);

/// <summary>
/// Raised when streamed content exceeds the caller's byte limit.
/// Derives from <see cref="InvalidOperationException"/> so existing avatar handling keeps its behavior.
/// </summary>
public sealed class FileSizeLimitExceededException(long maxBytes)
    : InvalidOperationException($"File exceeds the configured {maxBytes} byte limit.")
{
    public long MaxBytes { get; } = maxBytes;
}

public static class FileStreamUtilities
{
    public static async Task<BufferedFile> BufferAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        var target = new MemoryStream();
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                await target.DisposeAsync();
                throw new FileSizeLimitExceededException(maxBytes);
            }

            hasher.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        target.Position = 0;
        return new BufferedFile(target, total, Convert.ToHexString(hasher.GetHashAndReset()));
    }

    internal static async Task CopyToFileAsync(
        Stream source,
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var buffered = (await BufferAsync(source, maxBytes, cancellationToken)).Content;
        await using var target = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await buffered.CopyToAsync(target, cancellationToken);
    }
}
