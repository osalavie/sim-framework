using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.FileManagement;

public sealed record FileContent(
    string FileName,
    string ContentType,
    Stream Content,
    long Length);

public sealed record TabularExportRequest(
    string FileName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    TabularExportFormat Format);

public sealed record ImageEditRequest(
    string FileName,
    Stream Content,
    int? CropX = null,
    int? CropY = null,
    int? CropWidth = null,
    int? CropHeight = null,
    int? MaxWidth = null,
    int? MaxHeight = null,
    int Quality = 82,
    string? OutputExtension = null);

public interface IFileTemplateRenderer
{
    Task<string> RenderAsync(
        string template,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default);
}

public interface IFileExportService
{
    Task<FileContent> ExportAsync(
        TabularExportRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDocumentConverter
{
    Task<FileContent> ConvertToPdfAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface IImageProcessor
{
    Task<FileContent> ProcessAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken = default);
}
