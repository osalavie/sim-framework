using System.Collections.Concurrent;
using Sim.System.FileManagement;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// Represents a pending or completed file conversion task.
/// </summary>
public sealed record ConversionTask
{
    public required Guid Id { get; init; }
    public ConversionTaskType TaskType { get; init; }
    public required string InputFileName { get; init; }
    public required byte[] InputContent { get; init; }
    public string? OutputExtension { get; init; }
    public int Quality { get; init; } = 82;
    public int RetryCount { get; set; }
    public int MaxRetries { get; init; } = 3;
    public ConversionTaskStatus Status { get; set; } = ConversionTaskStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public byte[]? ResultContent { get; set; }
    public string? ResultFileName { get; set; }
    public string? ResultContentType { get; set; }
}

/// <summary>
/// Publishes conversion tasks to a background channel for async processing.
/// Decouples HTTP requests from heavy file processing.
/// </summary>
public interface IConversionQueue
{
    /// <summary>Enqueues a conversion task and returns a task ID for status polling.</summary>
    Task<ConversionTask> EnqueueAsync(ConversionTask task, CancellationToken cancellationToken = default);

    /// <summary>Gets the current status of a previously enqueued task.</summary>
    Task<ConversionTask?> GetStatusAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Reads a batch of pending tasks for a worker to process.</summary>
    IReadOnlyList<ConversionTask> DequeuePending(int batchSize = 5);

    /// <summary>Marks a task as completed with the result.</summary>
    void MarkCompleted(Guid taskId, FileContent result);

    /// <summary>Marks a task as failed. Moves to dead-letter after max retries.</summary>
    void MarkFailed(Guid taskId, string error);

    /// <summary>Retries dead-letter tasks by resetting them to pending.</summary>
    bool RetryDeadLetter(Guid taskId);

    /// <summary>Gets the result stream for a completed task. Returns null if not completed or not found.</summary>
    Task<FileContent?> GetResultAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryConversionQueue(ILogger<InMemoryConversionQueue> logger) : IConversionQueue
{
    private readonly ConcurrentDictionary<Guid, ConversionTask> _tasks = new();
    private readonly ConcurrentQueue<Guid> _pendingQueue = new();

    public Task<ConversionTask> EnqueueAsync(ConversionTask task, CancellationToken cancellationToken = default)
    {
        _tasks[task.Id] = task;
        _pendingQueue.Enqueue(task.Id);
        logger.LogDebug("Conversion task {TaskId} ({Type}) enqueued", task.Id, task.TaskType);
        return Task.FromResult(task);
    }

    public Task<ConversionTask?> GetStatusAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
            return Task.FromResult<ConversionTask?>(task);
        return Task.FromResult<ConversionTask?>(null);
    }

    public IReadOnlyList<ConversionTask> DequeuePending(int batchSize = 5)
    {
        var batch = new List<ConversionTask>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            if (!_pendingQueue.TryDequeue(out var taskId))
                break;

            if (!_tasks.TryGetValue(taskId, out var task))
                continue;

            task.Status = ConversionTaskStatus.Processing;
            task.StartedAt = DateTimeOffset.UtcNow;
            batch.Add(task);
        }
        return batch;
    }

    public void MarkCompleted(Guid taskId, FileContent result)
    {
        if (!_tasks.TryGetValue(taskId, out var task)) return;
        task.Status = ConversionTaskStatus.Completed;
        task.CompletedAt = DateTimeOffset.UtcNow;
        task.ResultContent = ((MemoryStream)result.Content).ToArray();
        task.ResultFileName = result.FileName;
        task.ResultContentType = result.ContentType;
        logger.LogInformation("Conversion task {TaskId} completed", taskId);
    }

    public void MarkFailed(Guid taskId, string error)
    {
        if (!_tasks.TryGetValue(taskId, out var task)) return;
        task.RetryCount++;
        if (task.RetryCount >= task.MaxRetries)
        {
            task.Status = ConversionTaskStatus.DeadLetter;
            task.CompletedAt = DateTimeOffset.UtcNow;
            logger.LogWarning("Conversion task {TaskId} moved to dead-letter after {Retries} retries: {Error}",
                taskId, task.RetryCount, error);
        }
        else
        {
            task.Status = ConversionTaskStatus.Pending;
            task.Error = error;
            _pendingQueue.Enqueue(taskId);
            logger.LogWarning("Conversion task {TaskId} failed (attempt {Attempt}/{Max}), re-queued: {Error}",
                taskId, task.RetryCount, task.MaxRetries, error);
        }
    }

    public bool RetryDeadLetter(Guid taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task)) return false;
        if (task.Status != ConversionTaskStatus.DeadLetter) return false;
        task.Status = ConversionTaskStatus.Pending;
        task.RetryCount = 0;
        task.Error = null;
        _pendingQueue.Enqueue(taskId);
        logger.LogInformation("Dead-letter task {TaskId} re-queued for retry", taskId);
        return true;
    }

    public Task<FileContent?> GetResultAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task)) return Task.FromResult<FileContent?>(null);
        if (task.Status != ConversionTaskStatus.Completed || task.ResultContent == null)
            return Task.FromResult<FileContent?>(null);
        return Task.FromResult<FileContent?>(
            new FileContent(
                task.ResultFileName ?? "result",
                task.ResultContentType ?? "application/octet-stream",
                new MemoryStream(task.ResultContent, writable: false),
                task.ResultContent.LongLength));
    }
}
