namespace Sim.DeviceAgent.Codecs;

public interface IFrameDecoder
{
    IReadOnlyList<DecodedFrame> Push(ReadOnlySpan<byte> data);

    IReadOnlyList<DecodedFrame> Flush();
}
