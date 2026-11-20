using System.Runtime.CompilerServices;
using Sim.DeviceAgent.Sources;

namespace Sim.DeviceAgent.Codecs;

public interface IDeviceCodec
{
    string Type { get; }

    IFrameDecoder CreateDecoder();

    IAsyncEnumerable<DecodedFrame> DecodeFileAsync(
        Stream stream,
        FileMetadata metadata,
        CancellationToken cancellationToken = default);
}

public abstract class DeviceCodecBase : IDeviceCodec
{
    public abstract string Type { get; }

    public abstract IFrameDecoder CreateDecoder();

    public virtual async IAsyncEnumerable<DecodedFrame> DecodeFileAsync(
        Stream stream,
        FileMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var decoder = CreateDecoder();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            foreach (var frame in decoder.Push(buffer.AsSpan(0, read)))
            {
                yield return frame;
            }
        }

        foreach (var frame in decoder.Flush())
        {
            yield return frame;
        }
    }
}
