namespace Sim.Framework.Application.Caching;

public sealed class CachingOptions
{
    public const string SectionName = "Framework:Caching";
    public string Provider { get; set; } = "Local";
    public string? RedisConnectionString { get; set; }
    public string InstanceName { get; set; } = "sim:";
}
