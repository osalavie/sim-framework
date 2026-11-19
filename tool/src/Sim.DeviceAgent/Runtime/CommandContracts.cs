using Sim.DeviceAgent.Codecs;

namespace Sim.DeviceAgent.Runtime;

public sealed record CommandRequest(string Text, TimeSpan Timeout, string? MatchPattern);

public sealed record CommandMatch(DecodedFrame Frame, long Seq, DateTimeOffset Timestamp);

public sealed record CommandOutcome(
    bool Success,
    bool TimedOut,
    CommandMatch? Match,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CommandOutcome Ok(CommandMatch match) => new(true, false, match, null, null);

    public static CommandOutcome Timeout() => new(false, true, null, "command_timeout", "The device did not return a matching frame before the timeout.");

    public static CommandOutcome Fail(string code, string message) => new(false, false, null, code, message);
}
