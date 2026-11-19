using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sim.Framework.Core.UnitOfWork;

namespace Sim.Framework.Core.Filters;

/// <summary>
/// Opens one SqlSugar transaction for actions explicitly marked with
/// <see cref="SqlSugarUnitOfWorkAttribute"/>.
/// </summary>
public sealed class UnitOfWorkFilter(
    ISqlSugarUnitOfWork unitOfWork,
    ILogger<UnitOfWorkFilter> logger) : IAsyncActionFilter, IOrderedFilter
{
    internal const int FilterOrder = 999;
    public int Order => FilterOrder;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor ||
            !actionDescriptor.MethodInfo.IsDefined(typeof(SqlSugarUnitOfWorkAttribute), true))
        {
            _ = await next();
            return;
        }

        logger.LogDebug("Beginning SqlSugar transaction for {Action}", actionDescriptor.DisplayName);
        await unitOfWork.BeginAsync(context.HttpContext.RequestAborted);

        try
        {
            var resultContext = await next();
            if (resultContext.Exception is null)
            {
                await unitOfWork.CommitAsync(context.HttpContext.RequestAborted);
            }
            else
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            // 事务提交/回滚后统一兜底关闭连接，防止请求级连接泄漏。
            await unitOfWork.CloseAsync(CancellationToken.None);
            logger.LogDebug("Completed SqlSugar transaction for {Action}", actionDescriptor.DisplayName);
        }
    }
}
