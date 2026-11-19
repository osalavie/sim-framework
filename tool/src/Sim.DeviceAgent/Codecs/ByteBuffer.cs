namespace Sim.DeviceAgent.Codecs;

internal sealed class ByteBuffer
{
    private byte[] _buffer = new byte[1024];
    private int _count;

    public int Count => _count;

    public ReadOnlySpan<byte> Span => _buffer.AsSpan(0, _count);

    public void Append(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_count + data.Length);
        data.CopyTo(_buffer.AsSpan(_count));
        _count += data.Length;
    }

    public void Consume(int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (count >= _count)
        {
            _count = 0;
            return;
        }

        Buffer.BlockCopy(_buffer, count, _buffer, 0, _count - count);
        _count -= count;
    }

    public void Clear() => _count = 0;

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        var next = _buffer.Length;
        while (next < required)
        {
            next *= 2;
        }

        Array.Resize(ref _buffer, next);
    }
}
