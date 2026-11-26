namespace Sim.Framework.Core.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int DefaultPermitLimit { get; set; } = 60;

    public int DefaultWindowSeconds { get; set; } = 60;

    public int QueueLimit { get; set; }
}
