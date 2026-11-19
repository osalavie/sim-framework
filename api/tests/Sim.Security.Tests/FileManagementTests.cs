using System.Security.Cryptography;
using Sim.System.Domain.Shared;
using System.Text;
using Microsoft.Extensions.Options;
using Sim.Framework.Core.Conventions;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.System.Application.Internal;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Application.FileManagement;
using Sim.System.Application.FileStorage;
using Sim.System.Application.ScheduleTask;
using Sim.System.Application.User;

namespace Sim.Security.Tests;

public sealed class FileManagementTests
{
    [Fact]
    public async Task Scriban_renderer_should_render_strict_template_models()
    {
        var renderer = new ScribanFileTemplateRenderer();

        var result = await renderer.RenderAsync(
            "Hello {{ name }}!{{ for item in items }} {{ item }}{{ end }}",
            new Dictionary<string, object?>
            {
                ["name"] = "SimFramework",
                ["items"] = new[] { "A", "B" }
            });

        Assert.Equal("Hello SimFramework! A B", result);
        var exception = await Record.ExceptionAsync(() =>
            renderer.RenderAsync("{{ missing }}", new Dictionary<string, object?>()));
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task Tabular_export_should_escape_spreadsheet_formulas_and_html()
    {
        var exporter = new FileExportService();
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?>
            {
                ["name"] = "=2+2",
                ["note"] = "<script>alert(1)</script>"
            }
        };

        var csv = await exporter.ExportAsync(new TabularExportRequest(
            "report",
            ["name", "note"],
            rows,
            TabularExportFormat.Csv));
        await using (csv.Content)
        using (var reader = new StreamReader(csv.Content, Encoding.UTF8))
        {
            var value = await reader.ReadToEndAsync();
            Assert.Contains("\"'=2+2\"", value);
        }

        var html = await exporter.ExportAsync(new TabularExportRequest(
            "report",
            ["name", "note"],
            rows,
            TabularExportFormat.Html));
        await using (html.Content)
        using (var reader = new StreamReader(html.Content, Encoding.UTF8))
        {
            var value = await reader.ReadToEndAsync();
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", value);
            Assert.DoesNotContain("<script>", value);
        }
    }

    [Fact]
    public async Task Local_storage_should_round_trip_and_reject_path_traversal()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "sim-file-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new LocalFileStorageProvider(
                new ConfiguredFileManagementOptionsProvider(Options.Create(new FileManagementOptions
                {
                    Local = new LocalFileStorageOptions { RootPath = rootPath }
                })));
            var bytes = Encoding.UTF8.GetBytes("enterprise file storage");
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes));

            await using (var source = new MemoryStream(bytes, writable: false))
            {
                var saved = await provider.SaveAsync(new FileStorageWriteRequest(
                    "2026/07/test.txt",
                    "test.txt",
                    "text/plain",
                    source,
                    bytes.LongLength,
                    sha256));
                Assert.Equal(FileStorageProviderNames.Local, saved.Provider);
                Assert.Equal(sha256, saved.Sha256);
            }

            Assert.True(await provider.ExistsAsync("2026/07/test.txt"));
            var opened = await provider.OpenReadAsync("2026/07/test.txt");
            Assert.NotNull(opened);
            await using (opened!.Content)
            using (var reader = new StreamReader(opened.Content, Encoding.UTF8))
            {
                Assert.Equal("enterprise file storage", await reader.ReadToEndAsync());
            }

            await Assert.ThrowsAsync<ArgumentException>(() =>
                provider.ExistsAsync("../outside.txt"));
            await provider.DeleteAsync("2026/07/test.txt");
            Assert.False(await provider.ExistsAsync("2026/07/test.txt"));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Local_storage_connection_test_should_probe_writable_directory()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "sim-storage-connection-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var result = await FileStorageConnectionTester.TestLocalAsync(
                rootPath,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(rootPath));
            Assert.Empty(Directory.EnumerateFileSystemEntries(rootPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Local_storage_connection_test_should_reject_invalid_paths()
    {
        var relative = await FileStorageConnectionTester.TestLocalAsync(
            "App_Data/files",
            CancellationToken.None);
        var missing = await FileStorageConnectionTester.TestLocalAsync(
            null,
            CancellationToken.None);

        Assert.False(relative.Success);
        Assert.False(missing.Success);
    }

    [Fact]
    public async Task Object_storage_connection_test_should_reject_incomplete_configuration()
    {
        var incomplete = await FileStorageConnectionTester.TestObjectStorageAsync(
            FileStorageProviderNames.S3,
            new S3CompatibleStorageOptions { Region = "us-east-1" },
            CancellationToken.None);
        var invalidEndpoint = await FileStorageConnectionTester.TestObjectStorageAsync(
            FileStorageProviderNames.S3,
            new S3CompatibleStorageOptions
            {
                Endpoint = "ftp://storage.example.com",
                Region = "us-east-1",
                Bucket = "documents",
                AccessKeyId = "access-key",
                AccessKeySecret = "access-secret"
            },
            CancellationToken.None);

        Assert.False(incomplete.Success);
        Assert.Contains("required", incomplete.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(invalidEndpoint.Success);
        Assert.Contains("HTTP", invalidEndpoint.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("users")]
    public async Task Avatar_storage_should_validate_content_and_use_a_public_owner_scoped_key(string scope)
    {
        var storage = new CapturingFileStorage();
        var controllerOptions = new ConventionalControllerOptions();
        controllerOptions.AddModule(typeof(AvatarStorage).Assembly, "system");
        var avatarStorage = new AvatarStorage(storage, controllerOptions);
        var ownerId = Guid.NewGuid();
        var webp = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2, 3, 4
        };

        await using (var content = new MemoryStream(webp, writable: false))
        {
            var saved = await avatarStorage.SaveAsync(scope, ownerId, content);

            Assert.Equal($"avatars/{scope}/{ownerId:N}.webp", saved.Key);
            Assert.Equal("image/webp", saved.ContentType);
            Assert.True(AvatarStorageKey.IsPublicAvatar(saved.Key));
            Assert.Contains($"/api/system/public-file/avatar", saved.Url, StringComparison.Ordinal);
            Assert.Contains(Uri.EscapeDataString(saved.Key), saved.Url, StringComparison.Ordinal);
        }

        await using var invalidContent = new MemoryStream("not-an-image"u8.ToArray(), writable: false);
        await Assert.ThrowsAsync<InvalidAvatarFileException>(() =>
            avatarStorage.SaveAsync(scope, ownerId, invalidContent));
    }

    [Theory]
    [InlineData("private/document.pdf")]
    [InlineData("avatars/users/00000000000000000000000000000000.webp")]
    public async Task Public_file_should_use_system_file_not_found_error_code(string key)
    {
        var service = new PublicFileService(new CapturingFileStorage());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetAvatarAsync(key, FileStorageProviderNames.Local));

        Assert.Equal(
            SystemErrorCodes.FileNotFound,
            exception.Code ?? exception.Message);
    }

    private sealed class CapturingFileStorage : IFileStorage
    {
        public Task<FileStorageWriteResult> SaveAsync(
            FileStorageWriteRequest request,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new FileStorageWriteResult(
                provider ?? FileStorageProviderNames.Local,
                request.Key,
                request.FileName,
                request.ContentType,
                request.Length,
                request.Sha256));

        public Task<FileStorageReadResult?> OpenReadAsync(
            string key,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.FromResult<FileStorageReadResult?>(null);

        public Task<FileAccessUrlResult?> GenerateAccessUrlAsync(
            string key,
            TimeSpan expiresIn,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.FromResult<FileAccessUrlResult?>(null);

        public Task<bool> ExistsAsync(
            string key,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task DeleteAsync(
            string key,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<FileUploadUrlResult?> GenerateUploadUrlAsync(
            string key,
            string fileName,
            string contentType,
            long length,
            TimeSpan expiresIn,
            string? provider = null,
            CancellationToken cancellationToken = default) => Task.FromResult<FileUploadUrlResult?>(null);
    }

    [Fact]
    public async Task Dynamic_schedule_security_should_block_private_http_and_unapproved_commands()
    {
        await Assert.ThrowsAsync<BusinessException>(() =>
            ScheduleTaskHttpTargetValidator.ValidateAsync("http://127.0.0.1/internal"));

        var policy = new ScheduleTaskCommandPolicy(Options.Create(new ScheduleTaskExecutionOptions()));
        Assert.Throws<BusinessException>(() => policy.ValidateCommand("unapproved-tool"));

        var allowedPolicy = new ScheduleTaskCommandPolicy(Options.Create(
            new ScheduleTaskExecutionOptions { AllowedCommands = ["dotnet"] }));
        Assert.Equal("dotnet", allowedPolicy.ValidateCommand("dotnet"));
        Assert.Throws<BusinessException>(() => allowedPolicy.ValidateCommand(
            Path.Combine(Path.GetTempPath(), "dotnet.exe")));
    }

    // ──────────────────── FileValidator tests ────────────────────

    [Fact]
    public void File_validator_should_accept_valid_png()
    {
        var validator = CreateValidator();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var (ext, mime) = ValidateStream(validator, "screenshot.png", png, "image/png");

        Assert.Equal(".png", ext);
        Assert.Equal("image/png", mime);
    }

    [Fact]
    public void File_validator_should_accept_valid_pdf()
    {
        var validator = CreateValidator();
        byte[] pdf = "%PDF-1.7 dummy"u8.ToArray();

        var (ext, mime) = ValidateStream(validator, "report.pdf", pdf, "application/pdf");

        Assert.Equal(".pdf", ext);
        Assert.Equal("application/pdf", mime);
    }

    [Fact]
    public void File_validator_should_reject_extension_mismatch_as_business_error()
    {
        var validator = CreateValidator();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "malicious.pdf", png, "image/png"));

        Assert.Equal(SystemErrorCodes.InvalidFileContent, ex.Code ?? ex.Message);
    }

    [Fact]
    public void File_validator_should_reject_dangerous_extension_as_business_error()
    {
        var validator = CreateValidator();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "payload.exe", png, "application/octet-stream"));

        Assert.Equal(SystemErrorCodes.FileTypeNotAllowed, ex.Code ?? ex.Message);
        Assert.Equal(".exe", ex.Data["Extension"]);
    }

    [Fact]
    public void File_validator_should_reject_content_type_mismatch_as_business_error()
    {
        var validator = CreateValidator();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "image.png", png, "text/html"));

        Assert.Equal(SystemErrorCodes.InvalidFileContent, ex.Code ?? ex.Message);
    }

    [Fact]
    public void File_validator_should_reject_missing_extension_as_business_error()
    {
        var validator = CreateValidator();

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "README", "text"u8.ToArray(), "text/plain"));

        Assert.Equal(SystemErrorCodes.FileExtensionMissing, ex.Code ?? ex.Message);
    }

    [Theory]
    [InlineData("readme.txt", "text/plain")]
    [InlineData("data.csv", "text/csv")]
    [InlineData("config.json", "application/json")]
    [InlineData("feed.xml", "application/xml")]
    [InlineData("style.css", "text/css")]
    [InlineData("query.sql", "application/sql")]
    public void File_validator_should_accept_text_formats_without_magic_header(string fileName, string expectedMime)
    {
        var validator = CreateValidator();

        var (extension, mime) = ValidateStream(
            validator,
            fileName,
            "第一行\r\nsecond line\n"u8.ToArray(),
            "application/octet-stream");

        Assert.Equal(Path.GetExtension(fileName), extension);
        Assert.Equal(expectedMime, mime);
    }

    [Fact]
    public void File_validator_should_reject_binary_text_disguised_as_text()
    {
        var validator = CreateValidator();
        byte[] pdf = "%PDF-1.7 dummy"u8.ToArray();

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "notes.txt", pdf, "text/plain"));

        Assert.Equal(SystemErrorCodes.InvalidFileContent, ex.Code ?? ex.Message);
    }

    [Fact]
    public void File_validator_should_reject_invalid_utf8_text_as_business_error()
    {
        var validator = CreateValidator();
        byte[] invalid = [.. "valid"u8.ToArray(), 0xFF];

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "notes.txt", invalid, "text/plain"));

        Assert.Equal(SystemErrorCodes.InvalidFileContent, ex.Code ?? ex.Message);
    }

    [Fact]
    public async Task File_stream_should_report_size_limit_with_a_dedicated_exception()
    {
        await using var content = new MemoryStream(new byte[32], writable: false);

        var ex = await Assert.ThrowsAsync<FileSizeLimitExceededException>(() =>
            FileStreamUtilities.BufferAsync(content, 16));

        Assert.Equal(16, ex.MaxBytes);
    }

    [Fact]
    public void File_validator_should_accept_octet_stream_fallback()
    {
        var validator = CreateValidator();
        byte[] pdf = "%PDF-1.7 dummy"u8.ToArray();

        // application/octet-stream is treated as "I don't know" — should pass
        var (ext, mime) = ValidateStream(validator, "doc.pdf", pdf, "application/octet-stream");

        Assert.Equal(".pdf", ext);
        Assert.Equal("application/pdf", mime);
    }

    [Fact]
    public void File_validator_should_reject_unrecognized_content_as_business_error()
    {
        var validator = CreateValidator();
        byte[] garbage = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "unknown.png", garbage, null));

        Assert.Equal(SystemErrorCodes.UnknownFileFormat, ex.Code ?? ex.Message);
    }

    [Fact]
    public void File_validator_should_reject_disallowed_extension_as_business_error()
    {
        var validator = CreateValidator();

        var ex = Assert.Throws<BusinessException>(() =>
            ValidateStream(validator, "unknown.bin", "binary"u8.ToArray(), null));

        Assert.Equal(SystemErrorCodes.FileTypeNotAllowed, ex.Code ?? ex.Message);
        Assert.Equal(".bin", ex.Data["Extension"]);
    }

    [Fact]
    public void Magic_detector_should_identify_jpeg()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        using var stream = new MemoryStream(jpeg, writable: false);
        var sig = FileMagicNumbers.Detect(stream);

        Assert.NotNull(sig);
        Assert.Equal(".jpg", sig!.Extension);
        Assert.Equal("image/jpeg", sig.MimeType);
    }

    [Fact]
    public void Magic_detector_should_return_null_for_unknown_bytes()
    {
        byte[] unknown = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        using var stream = new MemoryStream(unknown, writable: false);
        var sig = FileMagicNumbers.Detect(stream);

        Assert.Null(sig);
    }

    [Fact]
    public void Magic_detector_should_reset_stream_position()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        using var stream = new MemoryStream(png, writable: false);
        stream.Position = 0;
        FileMagicNumbers.Detect(stream);
        Assert.Equal(0, stream.Position);
    }

    private static IFileValidator CreateValidator() =>
        new FileValidator();

    private static (string Extension, string MimeType) ValidateStream(
        IFileValidator validator,
        string fileName,
        byte[] content,
        string? declaredContentType)
    {
        using var stream = new MemoryStream(content, writable: false);
        return validator.Validate(fileName, stream, declaredContentType);
    }
}
