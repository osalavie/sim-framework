namespace Sim.Security.Tests;

/// <summary>
/// 完整 Host 测试会使用 SqlSugar/Serilog 的进程级状态，必须串行运行以避免多个宿主互相覆盖配置。
/// </summary>
[CollectionDefinition("Sim host integration", DisableParallelization = true)]
public sealed class HostCollection
{
    public const string Name = "Sim host integration";
}
