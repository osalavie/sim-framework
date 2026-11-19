namespace Sim.System.Application.FileManagement;

/// <summary>
/// Wraps a stream to track the total bytes written during download,
/// invoking callbacks on completion or failure.
/// </summary>
internal sealed class DownloadTrackingStream : Stream
{
    private readonly Stream _inner;
    private readonly Func<long, Task> _onCompleted;
    private readonly Func<Exception, Task> _onFailed;
    private long _totalWritten;

    public DownloadTrackingStream(
        Stream inner,
        Func<long, Task> onCompleted,
        Func<Exception, Task> onFailed)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        _onFailed = onFailed ?? throw new ArgumentNullException(nameof(onFailed));
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        _totalWritten += read;
        if (read == 0)
        {
            _ = Task.Run(async () =>
            {
                try { await _onCompleted(_totalWritten); }
                catch { /* fire-and-forget */ }
            });
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            _totalWritten += read;
            if (read == 0)
            {
                await _onCompleted(_totalWritten);
            }
            return read;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _onFailed(ex);
            throw;
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
