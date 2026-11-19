using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Sim.Framework.Core.Filters;
using Xunit;

namespace Sim.Security.Tests;

/// <summary>
/// ModelState 校验响应守卫：参数不合法时必须带出字段级原因，而不是只返回通用提示。
/// </summary>
public class ModelStateValidationFilterTests
{
    [Fact]
    public async Task InvalidModelStateReturnsFieldLevelReasons()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");

        try
        {
            var context = new ActionExecutingContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: new object());
            context.ModelState.AddModelError("Path", "Path 只能包含字母、数字、连字符、下划线、正斜杠。");

            var filter = new ModelStateValidationFilter();
            await filter.OnActionExecutionAsync(
                context,
                () => throw new InvalidOperationException("无效模型不应继续执行"));

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);

            var json = JsonSerializer.Serialize(result.Value);
            using var document = JsonDocument.Parse(json);
            var error = document.RootElement.GetProperty("error");
            var message = error.GetProperty("message").GetString();

            Assert.Contains("请求参数不合法", message);
            Assert.Contains("Path 只能包含", message);
            Assert.Equal(JsonValueKind.Array, error.GetProperty("details").ValueKind);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
