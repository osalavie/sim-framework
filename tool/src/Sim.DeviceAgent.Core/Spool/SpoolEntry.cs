namespace Sim.DeviceAgent.Spool;

public sealed record SpoolEntry(string DeviceId, long Seq, string Json);
