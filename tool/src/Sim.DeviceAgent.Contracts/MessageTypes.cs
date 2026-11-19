namespace Sim.DeviceAgent.Contracts;

public static class MessageTypes
{
    public const string Hello = "hello";
    public const string HelloAck = "helloAck";
    public const string Subscribe = "subscribe";
    public const string Data = "data";
    public const string Status = "status";
    public const string Event = "event";
    public const string Command = "command";
    public const string Response = "response";
    public const string Ack = "ack";
    public const string Error = "error";
    public const string Ping = "ping";
    public const string Pong = "pong";
}
