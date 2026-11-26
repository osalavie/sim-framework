using System.Reflection;
using Sim.Framework.Core.Conventions;
using Sim.System.FileManagement;
using Sim.System.Application.FileManagement;

namespace Sim.System.Application.User;

public sealed class AvatarStorage(
    IFileStorage storage,
    ConventionalControllerOptions controllerOptions) : IAvatarStorage
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.Ordinal)
    {
        "users"
    };

    public async Task<StoredAvatar> SaveAsync(
        string scope,
        Guid ownerId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedScopes.Contains(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        BufferedFile buffered;
        try
        {
            buffered = await FileStreamUtilities.BufferAsync(
                content,
                AvatarStorageKey.MaxBytes,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidAvatarFileException(exception.Message, exception);
        }
        await using var bufferedContent = buffered.Content;
        (string extension, string contentType) image;
        try
        {
            image = DetectImage(bufferedContent);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidAvatarFileException(exception.Message, exception);
        }
        var (extension, contentType) = image;
        bufferedContent.Position = 0;
        var key = $"avatars/{scope}/{ownerId:N}{extension}";
        var saved = await storage.SaveAsync(
            new FileStorageWriteRequest(
                key,
                $"avatar{extension}",
                contentType,
                bufferedContent,
                buffered.Length,
                buffered.Sha256),
            cancellationToken: cancellationToken);
        // 头像 URL 必须与 AppServiceConvention 为 PublicFileService 生成的路由保持一致；
        // 通过查询当前程序集在 ConventionalControllerOptions 中登记的根路径，避免
        // 在 system 模块被改名后再次出现 404。
        var rootPath = controllerOptions.GetRootPath(typeof(AvatarStorage).Assembly);
        var url = $"/api/{rootPath}/public-file/avatar?provider={Uri.EscapeDataString(saved.Provider)}&key={Uri.EscapeDataString(saved.Key)}";
        return new StoredAvatar(url, saved.Provider, saved.Key, saved.ContentType, saved.Length);
    }

    private static (string Extension, string ContentType) DetectImage(Stream content)
    {
        Span<byte> header = stackalloc byte[12];
        var read = content.Read(header);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (read >= 8 && header[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return (".png", "image/png");
        }

        if (read >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return (".webp", "image/webp");
        }

        throw new InvalidOperationException("Avatar must be a valid JPEG, PNG or WebP image.");
    }
}
