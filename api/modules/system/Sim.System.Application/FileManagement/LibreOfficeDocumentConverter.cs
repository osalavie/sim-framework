using Sim.System.Application.Internal;
using Sim.System.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class LibreOfficeDocumentConverter(IFileManagementOptionsProvider optionsProvider)
    : IDocumentConverter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".odt", ".xls", ".xlsx", ".ods"
    };

    public async Task<FileContent> ConvertToPdfAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var options = await optionsProvider.GetAsync(cancellationToken);
        var tool = options.LibreOffice;
        if (!tool.Enabled)
        {
            throw new InvalidOperationException("LibreOffice document conversion is disabled.");
        }

        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeFileName);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException($"Document extension '{extension}' cannot be converted to PDF.");
        }

        using var workspace = new TemporaryWorkspace(options.TemporaryPath);
        var inputPath = workspace.Combine(safeFileName);
        var outputDirectory = workspace.Combine("output");
        var profileDirectory = workspace.Combine("libreoffice-profile");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(profileDirectory);
        await FileStreamUtilities.CopyToFileAsync(content, inputPath, options.MaxUploadBytes, cancellationToken);

        var profileUri = new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
        var result = await ExternalToolRunner.RunAsync(
            tool.ExecutablePath,
            [
                "--headless",
                "--nologo",
                "--nodefault",
                "--nolockcheck",
                "--norestore",
                $"-env:UserInstallation={profileUri}",
                "--convert-to",
                "pdf",
                "--outdir",
                outputDirectory,
                inputPath
            ],
            workspace.DirectoryPath,
            TimeSpan.FromSeconds(tool.TimeoutSeconds),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"LibreOffice conversion failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        var outputPath = Path.Combine(
            outputDirectory,
            Path.ChangeExtension(safeFileName, ".pdf"));
        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"LibreOffice did not produce the expected PDF. Output: {result.StandardOutput}");
        }

        var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
        return new FileContent(
            Path.ChangeExtension(safeFileName, ".pdf"),
            "application/pdf",
            new MemoryStream(bytes, writable: false),
            bytes.LongLength);
    }
}
