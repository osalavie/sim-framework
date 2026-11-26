namespace Sim.Framework.Core.UnitOfWork;

/// <summary>
/// Controls the SqlSugar transaction used by an HTTP application-service action.
/// ABP's unit of work continues to manage the ABP request lifecycle separately.
/// </summary>
public interface ISqlSugarUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭当前请求占用的数据库连接，作为事务提交/回滚后的兜底。
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
