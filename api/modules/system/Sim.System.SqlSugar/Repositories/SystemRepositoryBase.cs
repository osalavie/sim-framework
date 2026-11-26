using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Entities;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.SqlSugar.Repositories;

/// <summary>
/// Base repository for System module with enhanced error handling
/// Implements Requirement 9.1: Database error handling
/// </summary>
public abstract class SystemRepositoryBase<TEntity> : SqlSugarRepository<TEntity>
    where TEntity : class, IEntity, new()
{
    protected new readonly ILogger Logger;

    protected SystemRepositoryBase(ISqlSugarClient db, ILogger logger)
        : base(db)
    {
        Logger = logger;
    }

    /// <summary>
    /// Execute database operation with error handling
    /// Logs errors and throws user-friendly exceptions
    /// </summary>
    protected async Task<T> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            return await operation();
        }
        catch (SqlSugarException ex)
        {
            // Log the database error with full details
            Logger.LogError(
                ex,
                "Database error in {OperationName}: {Message}",
                operationName,
                ex.Message
            );

            // Throw user-friendly exception without exposing internal details
            throw new BusinessException(SystemErrorCodes.DatabaseError, "A database error occurred.");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            // Log unexpected errors
            Logger.LogError(
                ex,
                "Unexpected error in {OperationName}: {Message}",
                operationName,
                ex.Message
            );

            // Throw user-friendly exception
            throw new BusinessException(SystemErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Execute database operation with error handling (void return)
    /// </summary>
    protected async Task ExecuteWithErrorHandlingAsync(
        Func<Task> operation,
        string operationName)
    {
        try
        {
            await operation();
        }
        catch (SqlSugarException ex)
        {
            Logger.LogError(
                ex,
                "Database error in {OperationName}: {Message}",
                operationName,
                ex.Message
            );

            throw new BusinessException(SystemErrorCodes.DatabaseError, "A database error occurred.");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            Logger.LogError(
                ex,
                "Unexpected error in {OperationName}: {Message}",
                operationName,
                ex.Message
            );

            throw new BusinessException(SystemErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }
}
