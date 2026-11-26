namespace Sim.System.Application.Internal;

/// <summary>
/// Maps file magic byte signatures to MIME types and extensions.
/// Only covers formats commonly used in business file uploads.
/// </summary>
public static class FileMagicNumbers
{
    public sealed record Signature(string Extension, string MimeType);

    /// <summary>
    /// Detects the file type by reading the magic bytes from the stream head.
    /// Returns null when no known signature matches.
    /// The stream position is reset after detection.
    /// </summary>
    public static Signature? Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        var originalPosition = stream.CanSeek ? stream.Position : 0L;
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        // JPEG: FF D8 FF
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new Signature(".jpg", "image/jpeg");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (read >= 8 && header[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return new Signature(".png", "image/png");
        }

        // WebP: RIFF .... WEBP
        if (read >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return new Signature(".webp", "image/webp");
        }

        // GIF: GIF87a or GIF89a
        if (read >= 6 &&
            (header[..6].SequenceEqual("GIF87a"u8) ||
             header[..6].SequenceEqual("GIF89a"u8)))
        {
            return new Signature(".gif", "image/gif");
        }

        // BMP: BM
        if (read >= 2 && header[0] == 0x42 && header[1] == 0x4D)
        {
            return new Signature(".bmp", "image/bmp");
        }

        // PDF: %PDF-
        if (read >= 5 && header[..5].SequenceEqual("%PDF-"u8))
        {
            return new Signature(".pdf", "application/pdf");
        }

        // ZIP-based formats (DOCX, XLSX, ODT, ODS): PK 03 04
        if (read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
        {
            // We can't distinguish DOCX/XLSX/PPTX/ODT/ODS from the header alone,
            // so we return a generic ZIP signature and let extension whitelist
            // provide the context.
            return new Signature(".zip", "application/zip");
        }

        // Legacy OLE2-based DOC, XLS, PPT: D0 CF 11 E0 A1 B1 1A E1
        if (read >= 8 && header[..8].SequenceEqual(
                new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
        {
            return new Signature(".doc", "application/msword");
        }

        // Text-based: check for common text encodings
        // UTF-8 BOM: EF BB BF
        if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            return new Signature(".txt", "text/plain");
        }

        // UTF-16 LE BOM: FF FE
        if (read >= 2 && header[0] == 0xFF && header[1] == 0xFE)
        {
            return new Signature(".txt", "text/plain");
        }

        // JSON: starts with '{' or '['
        if (read >= 1 && (header[0] == (byte)'{' || header[0] == (byte)'['))
        {
            return new Signature(".json", "application/json");
        }

        // XML: starts with '<'
        if (read >= 1 && header[0] == (byte)'<')
        {
            return new Signature(".xml", "application/xml");
        }

        // CSV detection: content starts with printable ASCII + comma/tab
        // Skip - too unreliable, rely on extension whitelist.

        return null;
    }
}
