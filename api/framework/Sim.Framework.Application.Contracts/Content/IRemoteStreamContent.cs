namespace Sim.Framework.Application.Contracts.Content;

/// <summary>
/// Remote stream content abstraction. Replaces Volo.Abp.Content.IRemoteStreamContent.
/// Used for file upload/download across application service boundaries.
/// </summary>
public interface IRemoteStreamContent : IDisposable
{
    string? FileName { get; }

    string ContentType { get; }

    long? ContentLength { get; }

    Stream GetStream();
}
