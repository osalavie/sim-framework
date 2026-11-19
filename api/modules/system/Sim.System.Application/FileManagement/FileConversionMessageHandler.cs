using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.System.Domain.Shared;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// RabbitMQ message handler that processes file conversion tasks.
/// Reads the task from the database, loads input from storage, processes it,
/// and saves the result back to storage.
/// </summary>
public sealed class FileConversionMessageHandler(
    IServiceScopeFactory scopeFactory,
    ISqlSugarRepository<FileConversionTaskEntity> taskRepository,
    IFileStorage fileStorage,
    ILogger<FileConversionMessageHandler> logger) : IMessageHandler<Contracts.FileManagement.FileConversionMessage>
{
    public async Task HandleAsync(
        Contracts.FileManagement.FileConversionMessage message,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        var taskId = message.TaskId;
        logger.LogDebug("Processing conversion task {TaskId} ({Type})", taskId, message.TaskType);

        var entity = await taskRepository.GetAsync((object)taskId);
        if (entity == null)
        {
            logger.LogWarning("Conversion task {TaskId} not found in database", taskId);
            return;
        }

        if (entity.Status is ConversionTaskStatus.Completed or ConversionTaskStatus.DeadLetter)
        {
            logger.LogInformation("Conversion task {TaskId} already in terminal state {Status}, skipping", taskId, entity.Status);
            return;
        }

        // Mark as processing
        entity.Status = ConversionTaskStatus.Processing;
        entity.StartedAt = DateTimeOffset.UtcNow;
        entity.LastModificationTime = DateTime.UtcNow;
        await taskRepository.UpdateAsync(entity);

        try
        {
            // Read input from storage
            var inputResult = await fileStorage.OpenReadAsync(
                entity.InputStorageKey,
                entity.InputProvider,
                cancellationToken);
            if (inputResult == null)
            {
                throw new FileNotFoundException(
                    $"Input file not found in storage: {entity.InputStorageKey}");
            }

            using (inputResult.Content)
            {
                var result = entity.TaskType switch
                {
                    ConversionTaskType.DocumentToPdf => await ConvertDocumentAsync(entity, inputResult.Content, cancellationToken),
                    ConversionTaskType.ImageProcessing => await ProcessImageAsync(entity, inputResult.Content, cancellationToken),
                    _ => throw new InvalidOperationException($"Unknown conversion task type: {entity.TaskType}")
                };

                await SaveResultAsync(entity, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Conversion task {TaskId} failed", taskId);
            await MarkFailedAsync(entity, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<FileContent> ConvertDocumentAsync(
        FileConversionTaskEntity entity,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var converter = scope.ServiceProvider.GetRequiredService<IDocumentConverter>();
        return await converter.ConvertToPdfAsync(entity.InputFileName, content, cancellationToken);
    }

    private async Task<FileContent> ProcessImageAsync(
        FileConversionTaskEntity entity,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
        return await processor.ProcessAsync(
            new ImageEditRequest(
                entity.InputFileName,
                content,
                null, null, null, null, null, null,
                entity.Quality,
                entity.OutputExtension),
            cancellationToken);
    }

    private async Task SaveResultAsync(
        FileConversionTaskEntity entity,
        FileContent result,
        CancellationToken cancellationToken)
    {
        var resultKey = $"conversions/{entity.Id}/output/{result.FileName}";
        using var ms = new MemoryStream();
        await result.Content.CopyToAsync(ms, cancellationToken);
        var hash = SHA256.HashData(ms.ToArray());
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();
        ms.Position = 0;

        await fileStorage.SaveAsync(
            new FileStorageWriteRequest(
                resultKey, result.FileName, result.ContentType, ms, ms.Length, hexHash),
            "Local",
            cancellationToken);

        entity.Status = ConversionTaskStatus.Completed;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.ResultStorageKey = resultKey;
        entity.ResultFileName = result.FileName;
        entity.ResultContentType = result.ContentType;
        entity.ResultLength = ms.Length;
        entity.Error = null;
        entity.LastModificationTime = DateTime.UtcNow;
        await taskRepository.UpdateAsync(entity);

        logger.LogInformation("Conversion task {TaskId} completed, result saved to {Key}", entity.Id, resultKey);
    }

    private async Task MarkFailedAsync(
        FileConversionTaskEntity entity,
        string error,
        CancellationToken cancellationToken)
    {
        entity.RetryCount++;
        entity.Error = error;

        if (entity.RetryCount >= entity.MaxRetries)
        {
            entity.Status = ConversionTaskStatus.DeadLetter;
            entity.CompletedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            entity.Status = ConversionTaskStatus.Pending;
        }

        entity.LastModificationTime = DateTime.UtcNow;
        await taskRepository.UpdateAsync(entity);
    }
}
