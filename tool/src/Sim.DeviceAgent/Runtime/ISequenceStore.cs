namespace Sim.DeviceAgent.Runtime;

public interface ISequenceStore
{
    long Next(string deviceId);
}
