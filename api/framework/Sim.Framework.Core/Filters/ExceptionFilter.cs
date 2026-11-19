using System.Collections;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.ErrorCodes;
using Sim.Framework.Domain.Shared.Exceptions;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;

namespace Sim.Framework.Core.Filters;

/// <summary>
/// 将 Sim 框架异常映射为 HTTP 错误响应，替代 ABP 的异常处理中间件：
/// <list type="bullet">
/// <item><see cref="AuthorizationException"/> → 401 Unauthorized</item>
/// <item><see cref="ForbiddenException"/> → 403 Forbidden</item>
/// <item><see cref="BusinessException"/> → 400 BadRequest</item>
/// </list>
/// 响应体保持 ABP 风格的 error 包装结构，兼容现有前端错误处理。
/// </summary>
public sealed class ExceptionFilter : IAsyncExceptionFilter
{
    private readonly IStringLocalizerFactory _stringLocalizerFactory;
    private readonly AbpExceptionLocalizationOptions _localizationOptions;

    public ExceptionFilter(
        IStringLocalizerFactory stringLocalizerFactory,
        IOptions<AbpExceptionLocalizationOptions> localizationOptions)
    {
        _stringLocalizerFactory = stringLocalizerFactory;
        _localizationOptions = localizationOptions.Value;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (IsInvalidRequestPayload(context.Exception))
        {
            context.Result = new ObjectResult(new ErrorResponse(
                new ErrorInfo(FrameworkErrorCodes.InvalidInput, "请求参数格式不正确。")))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            context.ExceptionHandled = true;
            return Task.CompletedTask;
        }

        var isAuthorizationException = context.Exception is AuthorizationException;
        var isForbiddenException = context.Exception is ForbiddenException;
        if (!isAuthorizationException && !isForbiddenException &&
            context.Exception is not BusinessException)
        {
            return Task.CompletedTask;
        }

        var errorCode = ((IHasErrorCode)context.Exception).Code;
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            errorCode = null;
        }

        var message = context.Exception.Message;

        if (errorCode is null)
        {
            // 项目约定业务异常只传错误码（单参数构造），此时 Code 为空、
            // Message 是错误码；能命中本地化资源时需要在响应中还原 Code。
            if (TryLocalizeMessage(context.Exception.Message, context.Exception.Data, out var localizedFromMessage))
            {
                errorCode = context.Exception.Message;
                message = localizedFromMessage;
            }
        }
        else if (TryLocalizeMessage(errorCode, context.Exception.Data, out var localizedMessage))
        {
            message = localizedMessage;
        }

        context.Result = new ObjectResult(new ErrorResponse(
            new ErrorInfo(errorCode, message)))
        {
            StatusCode = isForbiddenException
                ? StatusCodes.Status403Forbidden
                : isAuthorizationException
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest
        };
        context.ExceptionHandled = true;

        return Task.CompletedTask;
    }

    private static bool IsInvalidRequestPayload(Exception exception)
    {
        return exception is JsonException or InputFormatterException ||
               exception is BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest };
    }

    private bool TryLocalizeMessage(string code, IDictionary data, out string message)
    {
        message = string.Empty;

        var namespaceSeparatorIndex = code.IndexOf(':');
        if (namespaceSeparatorIndex <= 0 ||
            !_localizationOptions.ErrorCodeNamespaceMappings.TryGetValue(
                code[..namespaceSeparatorIndex],
                out var resourceType) ||
            resourceType is null)
        {
            return false;
        }

        var localizedString = LocalizableString
            .Create(resourceType, code)
            .Localize(_stringLocalizerFactory);
        if (localizedString.ResourceNotFound)
        {
            return false;
        }

        var localizedMessage = localizedString.Value;
        foreach (DictionaryEntry item in data)
        {
            if (item.Key is not string key || item.Value is null)
            {
                continue;
            }

            localizedMessage = localizedMessage.Replace(
                $"{{{key}}}",
                Convert.ToString(item.Value, CultureInfo.CurrentUICulture) ?? string.Empty,
                StringComparison.Ordinal);
        }

        message = localizedMessage;
        return true;
    }

    private sealed record ErrorResponse(ErrorInfo Error);

    private sealed record ErrorInfo(string? Code, string Message);
}
