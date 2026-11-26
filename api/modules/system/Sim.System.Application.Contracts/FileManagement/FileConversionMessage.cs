using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.FileManagement;

/// <summary>
/// Message published to RabbitMQ for async file conversion processing.
/// The handler reads the full task from the database using <see cref="TaskId"/>.
/// </summary>
public sealed class FileConversionMessage
{
    public Guid TaskId { get; init; }
    public ConversionTaskTypeRef TaskType { get; init; }
}

/// <summary>
/// Mirrors <see cref="ConversionTaskType"/> for serialization.
/// </summary>
public enum ConversionTaskTypeRef
{
    DocumentToPdf,
    ImageProcessing
}

/// <summary>
/// Message type constants for file conversion messages.
/// </summary>
public static class FileConversionMessageTypes
{
    public const string Convert = "system.file-conversion.v1";
}
