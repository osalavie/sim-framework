namespace Sim.DeviceAgent.Contracts;

public sealed record SourceRef(string Name, long Size, string Sha256, int Index);
