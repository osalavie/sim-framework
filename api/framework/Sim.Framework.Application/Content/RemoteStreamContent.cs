using Sim.Framework.Application.Contracts.Content;

namespace Sim.Framework.Application.Content;

/// <summary>
/// Default IRemoteStreamContent implementation wrapping a Stream.
/// Replaces Volo.Abp.Content.RemoteStreamContent.
/// </summary>
public class RemoteStreamContent : IRemoteStreamContent
{
    private readonly Stream _stream;
    private readonly bool _disposeStream;

    public virtual string? FileName { get; }

    public virtual string ContentType { get; }

    public virtual long? ContentLength { get; }

    public RemoteStreamContent(
        Stream stream,
        string? fileName = null,
        string? contentType = null,
        long? contentLength = null,
        bool disposeStream = true)
    {
        _stream = stream;
        FileName = fileName;
        ContentType = contentType ?? "application/octet-stream";
        ContentLength = contentLength;
        _disposeStream = disposeStream;
    }

    public virtual Stream GetStream() => _stream;

    public void Dispose()
    {
        if (_disposeStream)
        {
            _stream.Dispose();
        }
    }
}
