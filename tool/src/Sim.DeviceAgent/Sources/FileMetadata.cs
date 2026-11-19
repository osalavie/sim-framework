namespace Sim.DeviceAgent.Sources;

public sealed record FileMetadata(string Name, long Size, string Sha256, DateTimeOffset LastWriteTime);
