using System.Runtime.CompilerServices;
using System.Text;
using Sim.DeviceAgent.Sources;

namespace Sim.DeviceAgent.Codecs;

public sealed class PassthroughCodec : DeviceCodecBase
{
    private readonly Encoding _encoding;

    public PassthroughCodec(Encoding encoding)
    {
        _encoding = encoding;
    }

    public override string Type => "passthrough";

    public override IFrameDecoder CreateDecoder() => new PassthroughDecoder(_encoding);

    public override async IAsyncEnumerable<DecodedFrame> DecodeFileAsync(
        Stream stream,
        FileMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        yield return FrameParser.Parse(buffer.ToArray(), _encoding, null);
    }

    private sealed class PassthroughDecoder : IFrameDecoder
    {
        private readonly Encoding _encoding;

        public PassthroughDecoder(Encoding encoding)
        {
            _encoding = encoding;
        }

        public IReadOnlyList<DecodedFrame> Push(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                return [];
            }

            var raw = data.ToArray();
            return [FrameParser.Parse(raw, _encoding, null)];
        }

        public IReadOnlyList<DecodedFrame> Flush() => [];
    }
}
