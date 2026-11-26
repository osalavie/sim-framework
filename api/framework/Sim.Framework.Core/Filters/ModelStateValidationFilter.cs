using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sim.Framework.Domain.Shared.ErrorCodes;

namespace Sim.Framework.Core.Filters;

/// <summary>
/// 对常规应用服务控制器统一执行 ModelState 校验。
/// 应用服务控制器没有 <c>[ApiController]</c>，不会自动把无效模型转成 400，
/// 这里补齐与前端错误结构一致的响应，并保留字段级校验原因。
/// </summary>
public sealed class ModelStateValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
            {
                var message = !string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? error.ErrorMessage
                    : error.Exception?.Message ?? "invalid value";

                return string.IsNullOrWhiteSpace(entry.Key) ||
                       message.Contains(entry.Key, StringComparison.OrdinalIgnoreCase)
                    ? message
                    : $"{entry.Key}: {message}";
            }))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        var message = errors.Count == 0
            ? (isChinese ? "请求参数不合法。" : "Invalid request parameters.")
            : (isChinese
                ? $"请求参数不合法：{string.Join("；", errors)}"
                : $"Invalid request parameters: {string.Join("; ", errors)}");

        context.Result = new ObjectResult(new
        {
            error = new
            {
                code = FrameworkErrorCodes.InvalidInput,
                message,
                details = errors
            }
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }
}
