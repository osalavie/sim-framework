using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Sim.Framework.Core.Filters;
using Volo.Abp.Localization.ExceptionHandling;
using System.Text;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Application.FileManagement;

namespace Sim.Security.Tests;

public sealed class MarkdownUploadValidationTests
{
    [Theory]
    [InlineData("text/markdown", "# 标题\n正文\n")]
    [InlineData("text/x-markdown", "[链接](https://example.test)")]
    [InlineData("text/plain", "<details>Markdown 中的 HTML</details>")]
    [InlineData("application/octet-stream", "{不是 JSON，也可以是 Markdown}")]
    [InlineData(null, "简短内容")]
    [InlineData("", "a")]
    [InlineData("text/markdown; charset=utf-8", "\t列表\r\n")]
    public void Accepts_utf8_markdown_without_magic_header(string? mime, string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var result = new FileValidator().Validate("article.MD", stream, mime);
        Assert.Equal((".md", "text/markdown"), result);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Accepts_utf8_bom_and_preserves_all_uploaded_bytes()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("# 标题\n正文")).ToArray();
        using var stream = new MemoryStream(bytes);
        Assert.Equal((".md", "text/markdown"), new FileValidator().Validate("readme.md", stream, "text/plain"));
        Assert.Equal(bytes, stream.ToArray());
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("nul")]
    [InlineData("invalid-utf8")]
    [InlineData("binary-signature")]
    [InlineData("mime")]
    public void Rejects_invalid_markdown_as_a_business_error(string reason)
    {
        var bytes = reason switch
        {
            "nul" => Encoding.UTF8.GetBytes(new string('a', 9000) + "\0"),
            "invalid-utf8" => Encoding.UTF8.GetBytes(new string('a', 9000)).Concat(new byte[] { 0xff }).ToArray(),
            "binary-signature" => "%PDF-1.7 fake document"u8.ToArray(),
            _ => "# Valid text"u8.ToArray()
        };
        using var stream = new MemoryStream(bytes);
        var error = Assert.Throws<BusinessException>(() => new FileValidator().Validate("readme.md", stream,
            reason == "mime" ? "image/png" : "text/markdown"));
        Assert.Equal(SystemErrorCodes.InvalidFileContent, error.Code ?? error.Message);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }
    [Fact]
    public async Task Invalid_markdown_returns_http_400_through_the_existing_exception_filter()
    {
        using var stream = new MemoryStream(new byte[] { 0xff });
        var exception = Assert.Throws<BusinessException>(() => new FileValidator().Validate("invalid.md", stream, "text/plain"));
        using var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var filter = new ExceptionFilter(services.GetRequiredService<IStringLocalizerFactory>(),
            Options.Create(new AbpExceptionLocalizationOptions()));
        var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new RouteData(),
            new ActionDescriptor()), new List<IFilterMetadata>()) { Exception = exception };
        await filter.OnExceptionAsync(context);
        Assert.True(context.ExceptionHandled);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

}
