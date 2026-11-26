using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sim.System.FileManagement;
using Sim.System.Application.FileManagement;
using Sim.System.Application.FileStorage;
using Sim.System.Application.User;

namespace Sim.System.Application;

public static class FileManagementServiceCollectionExtensions
{
    public static IServiceCollection AddFileManagement(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.AddOptions<FileManagementOptions>()
            .Bind(configuration.GetSection(FileManagementOptions.SectionName))
            .Validate(options => options.MaxUploadBytes > 0, "MaxUploadBytes must be positive.")
            .Validate(options => options.LibreOffice.TimeoutSeconds > 0, "LibreOffice timeout must be positive.")
            .Validate(options => options.ImageMagick.TimeoutSeconds > 0, "ImageMagick timeout must be positive.")
            .ValidateOnStart();
        services.AddOptions<ClamAvOptions>()
            .Bind(configuration.GetSection("FileManagement:ClamAV"))
            .Validate(options => options.Port > 0, "ClamAV port must be positive.");
        services.PostConfigure<FileManagementOptions>(options =>
        {
            options.Local.RootPath = ResolvePath(contentRootPath, options.Local.RootPath);
            options.TemporaryPath = ResolvePath(contentRootPath, options.TemporaryPath);
        });

        services.TryAddScoped<IFileStorage, FileManagement.FileStorage>();
        services.TryAddScoped<IAvatarStorage, AvatarStorage>();

        // Register ClamAV scanner when enabled; fall back to NoOp for development
        var clamAvEnabled = configuration.GetValue<bool>("FileManagement:ClamAV:Enabled");
        if (clamAvEnabled)
        {
            services.TryAddSingleton<IFileScanner, ClamAvFileScanner>();
        }
        else
        {
            services.TryAddSingleton<IFileScanner, NoOpFileScanner>();
        }

        services.TryAddSingleton<IFileValidator, FileValidator>();
        services.TryAddSingleton<IQuarantineService, InMemoryQuarantineService>();

        // Use distributed conversion queue when messaging is enabled (RabbitMQ/Kafka/RocketMQ);
        // fall back to in-memory queue for local development.
        var messagingProvider = configuration["Framework:Messaging:Provider"] ?? "Local";
        var messagingEnabled = configuration.GetValue<bool>("Framework:Messaging:Enabled");
        if (messagingEnabled && !string.Equals(messagingProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IConversionQueue, DistributedConversionQueue>();
        }
        else
        {
            services.TryAddSingleton<IConversionQueue, InMemoryConversionQueue>();
        }
        services.AddHostedService<ConversionBackgroundService>();
        services.TryAddScoped<IFileManagementOptionsProvider, ConfiguredFileManagementOptionsProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFileStorageProvider, LocalFileStorageProvider>());
        services.TryAddSingleton<IFileTemplateRenderer, ScribanFileTemplateRenderer>();
        services.TryAddSingleton<IFileExportService, FileExportService>();
        services.TryAddScoped<IDocumentConverter, LibreOfficeDocumentConverter>();
        services.TryAddScoped<IImageProcessor, ImageMagickImageProcessor>();
        return services;
    }

    private static string ResolvePath(string contentRootPath, string configuredPath) =>
        Path.GetFullPath(Path.IsPathFullyQualified(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath));
}
