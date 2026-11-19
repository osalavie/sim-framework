using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.Content;

namespace Sim.Framework.Application.OutputFormatters;

/// <summary>
/// 将 <see cref="IRemoteStreamContent"/> 结果写入响应体。
/// 替代 Volo.Abp.Content.RemoteStreamContentOutputFormatter，
/// 配合 ConventionalControllers 在文件下载接口（头像、原图等）使用。
/// </summary>
public sealed class RemoteStreamContentOutputFormatter : OutputFormatter
{
    public RemoteStreamContentOutputFormatter()
    {
        SupportedMediaTypes.Add("application/octet-stream");
    }

    protected override bool CanWriteType(Type? type) =>
        type == null || typeof(IRemoteStreamContent).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        if (context.Object is not IRemoteStreamContent content)
        {
            return;
        }

        var response = context.HttpContext.Response;
        response.ContentType = string.IsNullOrWhiteSpace(content.ContentType)
            ? "application/octet-stream"
            : content.ContentType;

        if (!string.IsNullOrEmpty(content.FileName))
        {
            // RFC 5987 编码文件名，兼容中文/特殊字符
            var encoded = Uri.EscapeDataString(content.FileName);
            response.Headers.ContentDisposition =
                $"attachment; filename=\"{encoded}\"; filename*=UTF-8''{encoded}";
        }

        if (content.ContentLength.HasValue && !response.Headers.ContainsKey("Content-Length"))
        {
            response.ContentLength = content.ContentLength;
        }

        var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()
            ?.CreateLogger<RemoteStreamContentOutputFormatter>();
        await using var stream = content.GetStream();
        await stream.CopyToAsync(response.Body);
        logger?.LogDebug(
            "Wrote remote stream content ({ContentType}) to response",
            response.ContentType);
    }
}