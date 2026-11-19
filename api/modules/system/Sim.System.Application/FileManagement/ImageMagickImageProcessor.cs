using Sim.System.Application.Internal;
using Sim.System.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class ImageMagickImageProcessor(IFileManagementOptionsProvider optionsProvider) : IImageProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public async Task<FileContent> ProcessAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = await optionsProvider.GetAsync(cancellationToken);
        var tool = options.ImageMagick;
        if (!tool.Enabled)
        {
            throw new InvalidOperationException("ImageMagick image processing is disabled.");
        }

        Validate(request);
        var safeFileName = Path.GetFileName(request.FileName);
        var inputExtension = Path.GetExtension(safeFileName);
        var outputExtension = NormalizeExtension(request.OutputExtension ?? inputExtension);
        if (!SupportedExtensions.Contains(inputExtension) || !SupportedExtensions.Contains(outputExtension))
        {
            throw new NotSupportedException("Only JPEG, PNG and WebP images are supported.");
        }

        using var workspace = new TemporaryWorkspace(options.TemporaryPath);
        var inputPath = workspace.Combine(safeFileName);
        var outputFileName = Path.ChangeExtension(Path.GetFileNameWithoutExtension(safeFileName) + "-edited", outputExtension);
        var outputPath = workspace.Combine(outputFileName);
        await FileStreamUtilities.CopyToFileAsync(
            request.Content,
            inputPath,
            options.MaxUploadBytes,
            cancellationToken);

        var arguments = new List<string> { inputPath, "-auto-orient" };
        if (request.CropWidth.HasValue)
        {
            arguments.Add("-crop");
            arguments.Add($"{request.CropWidth}x{request.CropHeight}+{request.CropX}+{request.CropY}");
            arguments.Add("+repage");
        }

        if (request.MaxWidth.HasValue || request.MaxHeight.HasValue)
        {
            arguments.Add("-resize");
            arguments.Add($"{request.MaxWidth?.ToString() ?? string.Empty}x{request.MaxHeight?.ToString() ?? string.Empty}>");
        }

        arguments.Add("-strip");
        arguments.Add("-quality");
        arguments.Add(request.Quality.ToString());
        arguments.Add(outputPath);

        var result = await ExternalToolRunner.RunAsync(
            tool.ExecutablePath,
            arguments,
            workspace.DirectoryPath,
            TimeSpan.FromSeconds(tool.TimeoutSeconds),
            cancellationToken);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"ImageMagick processing failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
        return new FileContent(
            outputFileName,
            GetContentType(outputExtension),
            new MemoryStream(bytes, writable: false),
            bytes.LongLength);
    }

    private static void Validate(ImageEditRequest request)
    {
        if (request.Quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Image quality must be between 1 and 100.");
        }

        var cropValues = new[] { request.CropX, request.CropY, request.CropWidth, request.CropHeight };
        if (cropValues.Any(value => value.HasValue) && cropValues.Any(value => !value.HasValue))
        {
            throw new ArgumentException("Crop X, Y, width and height must be supplied together.", nameof(request));
        }

        if (request.CropX < 0 || request.CropY < 0 || request.CropWidth <= 0 || request.CropHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Crop coordinates must describe a positive rectangle.");
        }

        if (request.MaxWidth <= 0 || request.MaxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Resize dimensions must be positive when supplied.");
        }
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

    private static string GetContentType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
