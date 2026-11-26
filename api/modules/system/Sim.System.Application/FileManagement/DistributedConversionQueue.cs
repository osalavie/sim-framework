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
/// Distributed implementation of <see cref="IConversionQueue"/> that persists tasks to the database
/// and publishes messages via <see cref="IMessageBus"/> for cross-instance processing.
/// File content is stored via <see cref="IFileStorage"/> instead of held in memory.
/// Uses <see cref="IServiceScopeFactory"/> to resolve scoped dependencies from this singleton.
/// </summary>
public sealed class DistributedConversionQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<DistributedConversionQueue> logger) : IConversionQueue
{
    private const string TempProvider = "Local";

    public async Task<ConversionTask> EnqueueAsync(
        ConversionTask task,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        // Save input content to temp storage
        var inputKey = $"conversions/{task.Id}/input/{task.InputFileName}";
        await using var inputStream = new MemoryStream(task.InputContent);
        var sha256 = ComputeSha256(task.InputContent);
        await fileStorage.SaveAsync(
            new FileStorageWriteRequest(
                inputKey,
                task.InputFileName,
                GuessContentType(task.InputFileName),
                inputStream,
                task.InputContent.LongLength,
                sha256),
            TempProvider,
            cancellationToken);

        // Persist task to database
        var entity = new FileConversionTaskEntity
        {
            Id = task.Id,
            TaskType = task.TaskType,
            InputFileName = task.InputFileName,
            InputStorageKey = inputKey,
            InputProvider = TempProvider,
            OutputExtension = task.OutputExtension,
            Quality = task.Quality,
            Status = ConversionTaskStatus.Pending,
            MaxRetries = task.MaxRetries,
            CreationTime = DateTime.UtcNow
        };
        await taskRepository.InsertAsync(entity);

        // Publish message for async processing
        await messageBus.PublishAsync(
            new Contracts.FileManagement.FileConversionMessage
            {
                TaskId = task.Id,
                TaskType = (Contracts.FileManagement.ConversionTaskTypeRef)task.TaskType
            },
            new MessagePublishOptions
            {
                Type = Contracts.FileManagement.FileConversionMessageTypes.Convert
            },
            cancellationToken);

        logger.LogInformation("Conversion task {TaskId} ({Type}) persisted and published", task.Id, task.TaskType);
        return task;
    }

    public async Task<ConversionTask?> GetStatusAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();
        var entity = await taskRepository.GetAsync((object)taskId);
        if (entity == null) return null;
        return MapToTask(entity);
    }

    /// <summary>Not used in distributed mode — messages arrive via RabbitMQ.</summary>
    public IReadOnlyList<ConversionTask> DequeuePending(int batchSize = 5) => Array.Empty<ConversionTask>();

    public void MarkCompleted(Guid taskId, FileContent result)
    {
        using var scope = scopeFactory.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();

        var entity = taskRepository.GetAsync((object)taskId).GetAwaiter().GetResult();
        if (entity == null) return;

        // Save result to temp storage
        var resultKey = $"conversions/{taskId}/output/{result.FileName}";
        using var ms = new MemoryStream();
        result.Content.CopyTo(ms);
        ms.Position = 0;
        var sha256 = ComputeSha256(ms.ToArray());
        fileStorage.SaveAsync(
            new FileStorageWriteRequest(
                resultKey, result.FileName, result.ContentType, ms, ms.Length, sha256),
            TempProvider).GetAwaiter().GetResult();

        entity.Status = ConversionTaskStatus.Completed;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.ResultStorageKey = resultKey;
        entity.ResultFileName = result.FileName;
        entity.ResultContentType = result.ContentType;
        entity.ResultLength = ms.Length;
        entity.LastModificationTime = DateTime.UtcNow;
        taskRepository.UpdateAsync(entity).GetAwaiter().GetResult();

        logger.LogInformation("Conversion task {TaskId} completed, result saved to {Key}", taskId, resultKey);
    }

    public void MarkFailed(Guid taskId, string error)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();

        var entity = taskRepository.GetAsync((object)taskId).GetAwaiter().GetResult();
        if (entity == null) return;

        entity.RetryCount++;
        entity.Error = error;

        if (entity.RetryCount >= entity.MaxRetries)
        {
            entity.Status = ConversionTaskStatus.DeadLetter;
            entity.CompletedAt = DateTimeOffset.UtcNow;
            logger.LogWarning("Conversion task {TaskId} moved to dead-letter after {Retries} retries: {Error}",
                taskId, entity.RetryCount, error);
        }
        else
        {
            entity.Status = ConversionTaskStatus.Pending;
            logger.LogWarning("Conversion task {TaskId} failed (attempt {Attempt}/{Max}): {Error}",
                taskId, entity.RetryCount, entity.MaxRetries, error);
        }

        entity.LastModificationTime = DateTime.UtcNow;
        taskRepository.UpdateAsync(entity).GetAwaiter().GetResult();
    }

    public bool RetryDeadLetter(Guid taskId)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var entity = taskRepository.GetAsync((object)taskId).GetAwaiter().GetResult();
        if (entity == null || entity.Status != ConversionTaskStatus.DeadLetter) return false;

        entity.Status = ConversionTaskStatus.Pending;
        entity.RetryCount = 0;
        entity.Error = null;
        entity.LastModificationTime = DateTime.UtcNow;
        taskRepository.UpdateAsync(entity).GetAwaiter().GetResult();

        messageBus.PublishAsync(
            new Contracts.FileManagement.FileConversionMessage
            {
                TaskId = entity.Id,
                TaskType = (Contracts.FileManagement.ConversionTaskTypeRef)entity.TaskType
            },
            new MessagePublishOptions
            {
                Type = Contracts.FileManagement.FileConversionMessageTypes.Convert
            }).GetAwaiter().GetResult();

        logger.LogInformation("Dead-letter task {TaskId} re-queued for retry", taskId);
        return true;
    }

    public async Task<FileContent?> GetResultAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ISqlSugarRepository<FileConversionTaskEntity>>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var entity = await taskRepository.GetAsync((object)taskId);
        if (entity == null || entity.Status != ConversionTaskStatus.Completed || string.IsNullOrEmpty(entity.ResultStorageKey))
            return null;

        var readResult = await fileStorage.OpenReadAsync(entity.ResultStorageKey, "Local", cancellationToken);
        if (readResult == null) return null;

        return new FileContent(
            readResult.FileName,
            readResult.ContentType,
            readResult.Content,
            readResult.Length);
    }

    private static ConversionTask MapToTask(FileConversionTaskEntity entity) => new()
    {
        Id = entity.Id,
        TaskType = entity.TaskType,
        InputFileName = entity.InputFileName,
        InputContent = Array.Empty<byte>(),
        OutputExtension = entity.OutputExtension,
        Quality = entity.Quality,
        RetryCount = entity.RetryCount,
        MaxRetries = entity.MaxRetries,
        Status = entity.Status,
        CreatedAt = entity.CreationTime,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        Error = entity.Error,
        ResultFileName = entity.ResultFileName,
        ResultContentType = entity.ResultContentType
    };

    private static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
