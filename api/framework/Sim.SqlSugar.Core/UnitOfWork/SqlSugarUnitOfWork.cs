using Microsoft.Extensions.Logging;
using Sim.Framework.Core.UnitOfWork;
using SqlSugar;

namespace Sim.SqlSugar.Core.UnitOfWork;

public sealed class SqlSugarUnitOfWork(
    ISqlSugarClient sqlSugarClient,
    ILogger<SqlSugarUnitOfWork>? logger = null) : ISqlSugarUnitOfWork
{
    private int _depth;

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_depth > 0)
        {
            _depth++;
            return;
        }

        try
        {
            await sqlSugarClient.AsTenant().BeginTranAsync();
            _depth = 1;
        }
        catch
        {
            await CloseConnectionAsync();
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_depth == 0)
        {
            return;
        }

        if (--_depth > 0)
        {
            return;
        }

        try
        {
            await sqlSugarClient.AsTenant().CommitTranAsync();
        }
        catch
        {
            // 提交失败时 Transaction 仍留在连接上，必须主动关闭，避免连接泄漏。
            await CloseConnectionAsync();
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_depth == 0)
        {
            return;
        }

        _depth = 0;
        try
        {
            await sqlSugarClient.AsTenant().RollbackTranAsync();
        }
        catch (Exception exception)
        {
            // 关闭连接本身会回滚未提交事务；记录回滚失败但不覆盖调用方的原始异常。
            logger?.LogError(exception, "SqlSugar 事务回滚失败，已强制关闭连接。");
            await CloseConnectionAsync();
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken = default) => CloseConnectionAsync();

    private async Task CloseConnectionAsync()
    {
        try
        {
            await sqlSugarClient.Ado.CloseAsync();
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "关闭 SqlSugar 连接失败。");
        }
    }
}
