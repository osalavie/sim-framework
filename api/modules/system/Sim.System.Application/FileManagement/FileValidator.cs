using System.Text;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Application.Internal;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// Validates uploaded files through a pipeline:
/// 1. Extension whitelist check
/// 2. Text byte validation or magic-number detection
/// 3. MIME type cross-validation
/// Validation failures are business errors so clients receive a localized HTTP 400 instead of a 500.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validates a file by its extension, stream content, and declared MIME type.
    /// On success, returns the validated (extension, mimeType).
    /// </summary>
    (string Extension, string MimeType) Validate(string fileName, Stream content, string? declaredContentType);
}

public sealed class FileValidator : IFileValidator
{
    private static readonly HashSet<string> DefaultAllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".odt", ".ods", ".odp",
        // Text
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm",
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        // Archives
        ".zip",
        // Code
        ".cs", ".ts", ".js", ".css", ".py", ".java", ".sql",
    };

    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib",
        ".bat", ".cmd", ".ps1", ".sh", ".bash",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".msi", ".scr", ".pif", ".com",
        ".php", ".jsp", ".asp", ".aspx",
        ".htm", ".html", ".shtml",
    };

    // Text formats have no reliable magic signature: UTF-8 without a BOM would
    // otherwise be treated as an unknown binary format.
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".csv", ".json", ".xml", ".css", ".js", ".ts", ".py", ".java", ".sql", ".cs",
    };

    public (string Extension, string MimeType) Validate(
        string fileName,
        Stream content,
        string? declaredContentType)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(content));
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new BusinessException(SystemErrorCodes.FileExtensionMissing);
        }

        // 1. Extension whitelist check
        ValidateExtension(extension);

        // Text formats have no fixed magic signature; validate their bytes instead.
        if (TextExtensions.Contains(extension))
        {
            return ValidateText(content, extension, declaredContentType);
        }

        // 2. Magic-number detection
        var detected = FileMagicNumbers.Detect(content);
        if (detected is null)
        {
            throw new BusinessException(SystemErrorCodes.UnknownFileFormat);
        }

        // 3. MIME type cross-validation (when both are declared by the client)
        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !IsCompatibleMimeType(detected.MimeType, declaredContentType))
        {
            throw new BusinessException(SystemErrorCodes.InvalidFileContent);
        }

        // 4. Extension vs magic-number consistency check
        if (!string.Equals(extension, detected.Extension, StringComparison.OrdinalIgnoreCase))
        {
            // For ZIP-based Office formats, the extension is the authoritative indicator
            if (detected.Extension == ".zip" && IsAllowedExtension(extension) &&
                (IsOfficeExtension(extension) || IsOpenDocumentExtension(extension)))
            {
                // ZIP magic is expected for Office Open XML and ODF formats
                return (extension, DeriveMimeTypeFromExtension(extension));
            }

            // For OLE2-based formats, accept legacy .doc/.xls
            if (detected.Extension == ".doc" && IsAllowedExtension(extension) &&
                extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
            {
                return (extension, DeriveMimeTypeFromExtension(extension));
            }

            throw new BusinessException(SystemErrorCodes.InvalidFileContent);
        }

        // Success: use the detected MIME type (not the client-declared one)
        return (detected.Extension, detected.MimeType);
    }

    private static (string Extension, string MimeType) ValidateText(
        Stream content,
        string extension,
        string? declaredContentType)
    {
        if (!content.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(content));
        var normalizedExtension = extension.ToLowerInvariant();
        var expectedMimeType = DeriveTextMimeType(normalizedExtension);
        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !IsCompatibleTextMimeType(expectedMimeType, declaredContentType))
        {
            throw new BusinessException(SystemErrorCodes.InvalidFileContent);
        }

        var position = content.Position;
        try
        {
            var signature = FileMagicNumbers.Detect(content);
            if (signature != null && signature.Extension is not (".txt" or ".json" or ".xml"))
                throw new BusinessException(SystemErrorCodes.InvalidFileContent);

            // Strict UTF-8 (including its optional BOM), bounded working memory,
            // and a full-file scan so binary bytes cannot hide after a valid prefix.
            using var reader = new StreamReader(content, new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            var characters = new char[4096];
            int read;
            while ((read = reader.Read(characters, 0, characters.Length)) > 0)
            {
                for (var index = 0; index < read; index++)
                {
                    var character = characters[index];
                    if (char.IsControl(character) && character is not ('\r' or '\n' or '\t'))
                        throw new BusinessException(SystemErrorCodes.InvalidFileContent);
                }
            }
        }
        catch (DecoderFallbackException)
        {
            throw new BusinessException(SystemErrorCodes.InvalidFileContent);
        }
        finally { content.Position = position; }

        return (normalizedExtension, expectedMimeType);
    }

    private static string DeriveTextMimeType(string extension) => extension switch
    {
        ".md" => "text/markdown",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".css" => "text/css",
        ".js" => "text/javascript",
        ".sql" => "application/sql",
        _ => "text/plain"
    };

    private static bool IsCompatibleTextMimeType(string expected, string declared)
    {
        var mediaType = declared.Split(';', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return true;
        }

        if (mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
            mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (expected == "application/json" &&
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (expected == "application/xml" &&
            mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Excel exports CSV with its own workbook MIME type.
        return expected == "text/csv" &&
               mediaType.Equals("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateExtension(string extension)
    {
        if (DangerousExtensions.Contains(extension) || !IsAllowedExtension(extension))
        {
            throw new BusinessException(SystemErrorCodes.FileTypeNotAllowed)
                .WithData("Extension", extension.ToLowerInvariant());
        }
    }

    private static bool IsAllowedExtension(string extension) =>
        DefaultAllowedExtensions.Contains(extension);

    private static bool IsOfficeExtension(string extension) =>
        extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenDocumentExtension(string extension) =>
        extension.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".ods", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".odp", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompatibleMimeType(string detected, string declared) =>
        string.Equals(detected, declared, StringComparison.OrdinalIgnoreCase) ||
        // Accept generic fallbacks
        declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
        // ZIP-based Office formats may declare specific OOXML MIME types
        (detected == "application/zip" && (declared.StartsWith("application/vnd.openxmlformats", StringComparison.OrdinalIgnoreCase) ||
                                           declared.StartsWith("application/vnd.oasis.opendocument", StringComparison.OrdinalIgnoreCase))) ||
        // OLE2-based can be doc or xls
        (detected == "application/msword" &&
         declared.Equals("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase));

    private static string DeriveMimeTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        ".odp" => "application/vnd.oasis.opendocument.presentation",
        ".xls" => "application/vnd.ms-excel",
        _ => "application/octet-stream"
    };
}
