namespace Sim.Framework.Domain.Shared.Data;

/// <summary>
/// 数据种子步骤描述。Key 供前端展示初始化进度，Name 用于服务端日志。
/// </summary>
public sealed record DataSeedStep(string Key, string Name);

/// <summary>
/// Data seed contributor interface. Replaces Volo.Abp.Data.IDataSeedContributor.
/// Implementations are executed on application startup to seed initial data.
/// </summary>
public interface IDataSeedContributor
{
    /// <summary>
    /// 执行过程中按顺序上报的步骤。
    /// </summary>
    IReadOnlyList<DataSeedStep> Steps { get; }

    /// <summary>
    /// 执行种子。onStep 在每一步开始前回调，没有进度订阅时传 null。
    /// </summary>
    Task SeedAsync(Action<DataSeedStep>? onStep = null);
}
