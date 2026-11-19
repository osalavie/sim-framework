using System.Net;
using Sim.System.FileManagement;
using System.Text;
using System.Text.Json;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.FileManagement;

public sealed class FileExportService : IFileExportService
{
    public Task<FileContent> ExportAsync(
        TabularExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one export column is required.", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = request.Format switch
        {
            TabularExportFormat.Csv => ExportCsv(request),
            TabularExportFormat.Json => ExportJson(request),
            TabularExportFormat.Html => ExportHtml(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        return Task.FromResult(result);
    }

    private static FileContent ExportCsv(TabularExportRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', request.Columns.Select(EscapeCsv)));
        foreach (var row in request.Rows)
        {
            builder.AppendLine(string.Join(',', request.Columns.Select(column =>
                EscapeCsv(row.TryGetValue(column, out var value) ? value : null))));
        }

        return CreateTextFile(request.FileName, ".csv", "text/csv; charset=utf-8", builder.ToString());
    }

    private static FileContent ExportJson(TabularExportRequest request)
    {
        var normalizedRows = request.Rows.Select(row => request.Columns.ToDictionary(
            column => column,
            column => row.TryGetValue(column, out var value) ? value : null,
            StringComparer.OrdinalIgnoreCase));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalizedRows, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return CreateFile(request.FileName, ".json", "application/json; charset=utf-8", bytes);
    }

    private static FileContent ExportHtml(TabularExportRequest request)
    {
        var builder = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>");
        builder.Append(WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(request.FileName)));
        builder.Append("</title></head><body><table><thead><tr>");
        foreach (var column in request.Columns)
        {
            builder.Append("<th>").Append(WebUtility.HtmlEncode(column)).Append("</th>");
        }

        builder.Append("</tr></thead><tbody>");
        foreach (var row in request.Rows)
        {
            builder.Append("<tr>");
            foreach (var column in request.Columns)
            {
                row.TryGetValue(column, out var value);
                builder.Append("<td>").Append(WebUtility.HtmlEncode(value)).Append("</td>");
            }

            builder.Append("</tr>");
        }

        builder.Append("</tbody></table></body></html>");
        return CreateTextFile(request.FileName, ".html", "text/html; charset=utf-8", builder.ToString());
    }

    private static string EscapeCsv(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.Length > 0 && safeValue[0] is '=' or '+' or '-' or '@')
        {
            safeValue = "'" + safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }

    private static FileContent CreateTextFile(
        string fileName,
        string extension,
        string contentType,
        string value) =>
        CreateFile(fileName, extension, contentType, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(value));

    private static FileContent CreateFile(
        string fileName,
        string extension,
        string contentType,
        byte[] bytes)
    {
        var normalizedFileName = Path.ChangeExtension(Path.GetFileName(fileName), extension);
        return new FileContent(normalizedFileName, contentType, new MemoryStream(bytes, writable: false), bytes.LongLength);
    }
}
