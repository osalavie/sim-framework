using System.Collections;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.Filters;

/// <summary>
/// Global exception filter for System module
/// Handles exceptions and returns consistent error responses
/// </summary>
public class SystemExceptionFilter : IExceptionFilter
{
    private readonly ILogger<SystemExceptionFilter> _logger;

    public SystemExceptionFilter(ILogger<SystemExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        // Only handle System module exceptions
        if (context.Exception is not SystemModuleException)
        {
            return;
        }

        var exception = (SystemModuleException)context.Exception;
        
        // Determine HTTP status code based on error code
        var statusCode = GetStatusCode(exception.Code ?? SystemErrorCodes.InternalError);
        
        // Log the exception (Requirement 9.1)
        LogException(exception, statusCode);

        // Create error response (Requirement 9.2, 9.3, 9.4, 9.5)
        var errorResponse = new
        {
            error = new
            {
                code = exception.Code,
                message = exception.Message,
                details = exception.Data.Count > 0 
                    ? exception.Data.Cast<DictionaryEntry>()
                        .ToDictionary(e => e.Key.ToString()!, e => e.Value)
                    : new Dictionary<string, object?>()
            }
        };

        context.Result = new ObjectResult(errorResponse)
        {
            StatusCode = (int)statusCode
        };

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Determine HTTP status code based on error code
    /// </summary>
    private HttpStatusCode GetStatusCode(string errorCode)
    {
        return errorCode switch
        {
            SystemErrorCodes.ModuleNotFound => HttpStatusCode.NotFound,
            SystemErrorCodes.RouteNotFound => HttpStatusCode.NotFound,
            SystemErrorCodes.ModuleAccessDenied => HttpStatusCode.Forbidden,
            SystemErrorCodes.InvalidModuleData => HttpStatusCode.BadRequest,
            SystemErrorCodes.InvalidRouteData => HttpStatusCode.BadRequest,
            SystemErrorCodes.DatabaseError => HttpStatusCode.InternalServerError,
            SystemErrorCodes.InternalError => HttpStatusCode.InternalServerError,
            _ => HttpStatusCode.InternalServerError
        };
    }

    /// <summary>
    /// Log exception with appropriate level
    /// Requirement 9.1: Log errors without exposing sensitive information
    /// </summary>
    private void LogException(SystemModuleException exception, HttpStatusCode statusCode)
    {
        var logLevel = statusCode switch
        {
            HttpStatusCode.BadRequest => LogLevel.Warning,
            HttpStatusCode.NotFound => LogLevel.Warning,
            HttpStatusCode.Forbidden => LogLevel.Warning,
            _ => LogLevel.Error
        };

        // Create sanitized data dictionary (remove sensitive information)
        var sanitizedData = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in exception.Data)
        {
            var key = entry.Key.ToString()!;
            // Don't log sensitive fields
            if (!IsSensitiveField(key))
            {
                sanitizedData[key] = entry.Value;
            }
        }

        _logger.Log(
            logLevel,
            exception,
            "System module exception: {ErrorCode} - {Message}. Data: {@Data}",
            exception.Code,
            exception.Message,
            sanitizedData
        );
    }

    /// <summary>
    /// Check if a field name is sensitive and should not be logged
    /// Requirement 11.4: Don't log sensitive information
    /// </summary>
    private bool IsSensitiveField(string fieldName)
    {
        var sensitiveFields = new[]
        {
            "password",
            "token",
            "secret",
            "key",
            "credential",
            "authorization"
        };

        return sensitiveFields.Any(s => 
            fieldName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
