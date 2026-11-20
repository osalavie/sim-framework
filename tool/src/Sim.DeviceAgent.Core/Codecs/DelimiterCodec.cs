using System.Text;
using System.Text.RegularExpressions;

namespace Sim.DeviceAgent.Codecs;

public sealed class DelimiterCodec : DeviceCodecBase
{
    private readonly byte[] _delimiter;
    private readonly Encoding _encoding;
    private readonly Regex? _parser;
    private readonly int _maxFrameBytes;

    public DelimiterCodec(string delimiter, Encoding encoding, Regex? parser, int maxFrameBytes)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            throw new ArgumentException("Delimiter cannot be empty.", nameof(delimiter));
        }

        _delimiter = Encoding.UTF8.GetBytes(delimiter);
        _encoding = encoding;
        _parser = parser;
        _maxFrameBytes = maxFrameBytes;
    }

    public override string Type => "delimiter";

    public override IFrameDecoder CreateDecoder() => new DelimiterDecoder(_delimiter, _encoding, _parser, _maxFrameBytes);

    private sealed class DelimiterDecoder : IFrameDecoder
    {
        private readonly byte[] _delimiter;
        private readonly Encoding _encoding;
        private readonly Regex? _parser;
        private readonly int _maxFrameBytes;
        private readonly ByteBuffer _buffer = new();

        public DelimiterDecoder(byte[] delimiter, Encoding encoding, Regex? parser, int maxFrameBytes)
        {
            _delimiter = delimiter;
            _encoding = encoding;
            _parser = parser;
            _maxFrameBytes = maxFrameBytes;
        }

        public IReadOnlyList<DecodedFrame> Push(ReadOnlySpan<byte> data)
        {
            _buffer.Append(data);
            var frames = new List<DecodedFrame>();

            while (true)
            {
                var span = _buffer.Span;
                var delimiterIndex = span.IndexOf(_delimiter);
                if (delimiterIndex >= 0)
                {
                    var raw = span[..delimiterIndex].ToArray();
                    _buffer.Consume(delimiterIndex + _delimiter.Length);
                    frames.Add(FrameParser.Parse(raw, _encoding, _parser));
                    continue;
                }

                if (_buffer.Count > _maxFrameBytes)
                {
                    var raw = _buffer.Span[.._maxFrameBytes].ToArray();
                    _buffer.Clear();
                    frames.Add(new DecodedFrame(raw, null, null, "frame_too_large"));
                }

                break;
            }

            return frames;
        }

        public IReadOnlyList<DecodedFrame> Flush()
        {
            if (_buffer.Count == 0)
            {
                return [];
            }

            var raw = _buffer.Span.ToArray();
            _buffer.Clear();
            return [FrameParser.Parse(raw, _encoding, _parser)];
        }
    }
}
