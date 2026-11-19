namespace Sim.DeviceAgent.Contracts;

public sealed record MessageError(string Code, string Message, bool Retryable = false);
