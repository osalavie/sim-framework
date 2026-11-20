namespace Sim.DeviceAgent.Configuration;

public sealed class AgentConfigFile
{
    public AgentConfigFile(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }
}
