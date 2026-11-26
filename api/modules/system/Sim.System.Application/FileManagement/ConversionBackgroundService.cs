using Microsoft.Extensions.DependencyInjection;
using Sim.System.FileManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// Background service that processes queued file conversion tasks
/// and periodically scans quarantined files for malware.
/// Runs in-process for single-instance deployments;
/// move to a separate Worker process for production isolation.
/// </summary>
public sealed class ConversionBackgroundService(
    IConversionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ConversionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Conversion background service started");

        var lastScan = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);

                if (DateTimeOffset.UtcNow - lastScan >= ScanInterval)
                {
                    await ScanQuarantineAsync(stoppingToken);
                    lastScan = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in background service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        logger.LogInformation("Conversion background service stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = queue.DequeuePending(batchSize: 3);
        if (batch.Count == 0) return;

        foreach (var task in batch)
        {
            try
            {
                await ProcessTaskAsync(task, cancellationToken);
            }
            catch (Exception ex)
            {
                queue.MarkFailed(task.Id, ex.Message);
            }
        }
    }

    private async Task ProcessTaskAsync(ConversionTask task, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await using var content = new MemoryStream(task.InputContent, writable: false);

        var result = task.TaskType switch
        {
            ConversionTaskType.DocumentToPdf => await ConvertDocumentAsync(scope, task, content, cancellationToken),
            ConversionTaskType.ImageProcessing => await ProcessImageAsync(scope, task, content, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown conversion task type: {task.TaskType}")
        };

        queue.MarkCompleted(task.Id, result);
    }

    private static async Task<FileContent> ConvertDocumentAsync(
        IServiceScope scope,
        ConversionTask task,
        Stream content,
        CancellationToken cancellationToken)
    {
        var converter = scope.ServiceProvider.GetRequiredService<IDocumentConverter>();
        return await converter.ConvertToPdfAsync(task.InputFileName, content, cancellationToken);
    }

    private static async Task<FileContent> ProcessImageAsync(
        IServiceScope scope,
        ConversionTask task,
        Stream content,
        CancellationToken cancellationToken)
    {
        var processor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
        return await processor.ProcessAsync(
            new ImageEditRequest(
                task.InputFileName,
                content,
                null, null, null, null, null, null,
                task.Quality,
                task.OutputExtension),
            cancellationToken);
    }

    private async Task ScanQuarantineAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var quarantineService = scope.ServiceProvider.GetRequiredService<IQuarantineService>();
            var fileScanner = scope.ServiceProvider.GetRequiredService<IFileScanner>();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

            var pending = await quarantineService.ListByStatusAsync(QuarantineStatus.Pending, limit: 10, cancellationToken);
            foreach (var record in pending)
            {
                try
                {
                    await quarantineService.UpdateStatusAsync(record.Key, QuarantineStatus.Scanning,
                        scannerName: fileScanner.Name, cancellationToken: cancellationToken);

                    var readResult = await fileStorage.OpenReadAsync(record.Key, record.Provider, cancellationToken);
                    if (readResult == null)
                    {
                        await quarantineService.UpdateStatusAsync(record.Key, QuarantineStatus.ScanError,
                            scanResult: "File not found in storage", cancellationToken: cancellationToken);
                        continue;
                    }

                    await using (readResult.Content)
                    {
                        var scanResult = await fileScanner.ScanAsync(readResult.Content, cancellationToken);

                        if (scanResult == null)
                        {
                            await quarantineService.UpdateStatusAsync(record.Key, QuarantineStatus.Clean,
                                cancellationToken: cancellationToken);
                            logger.LogDebug("File {Key} scanned clean by {Scanner}", record.Key, fileScanner.Name);
                        }
                        else
                        {
                            await quarantineService.UpdateStatusAsync(record.Key, QuarantineStatus.Infected,
                                scanResult: scanResult, cancellationToken: cancellationToken);
                            logger.LogWarning("Malware detected in file {Key} by {Scanner}: {Result}",
                                record.Key, fileScanner.Name, scanResult);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Scan error for file {Key}", record.Key);
                    await quarantineService.UpdateStatusAsync(record.Key, QuarantineStatus.ScanError,
                        scanResult: ex.Message, cancellationToken: CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error in quarantine scan loop");
        }
    }
}
