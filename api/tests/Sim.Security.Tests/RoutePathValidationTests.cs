using System.ComponentModel.DataAnnotations;
using Sim.System.Application.Contracts.Validation;
using Xunit;

namespace Sim.Security.Tests;

/// <summary>
/// 路由路径校验守卫：动态段（:id、:id?、自定义正则与通配符）必须可用，
/// 同时继续拒绝连续斜杠、尾斜杠和空白。
/// </summary>
public class RoutePathValidationTests
{
    [Theory]
    [InlineData("editor/:id?")]
    [InlineData("detail/:instanceId")]
    [InlineData("definition/edit/:definitionId?")]
    [InlineData("data-dictionary-data/:dictionaryId")]
    [InlineData(":pathMatch(.*)*")]
    [InlineData("system/file-storage")]
    [InlineData("/")]
    public void DynamicAndStaticPathsAreValid(string path)
    {
        Assert.Null(Validate(path));
    }

    [Theory]
    [InlineData("foo//bar")]
    [InlineData("foo/")]
    [InlineData("foo bar")]
    [InlineData("foo/:id?bar")]
    public void MalformedPathsAreRejected(string path)
    {
        var result = Validate(path);
        Assert.NotNull(result);
        Assert.Contains("Path", result!.ErrorMessage);
    }

    private static ValidationResult? Validate(string path)
    {
        var context = new ValidationContext(new object()) { DisplayName = "Path" };
        return new ValidRoutePathAttribute().GetValidationResult(path, context);
    }
}
