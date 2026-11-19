using Sim.Framework.Domain.Shared.Numbering;

namespace Sim.Security.Tests;

public sealed class NumberFormatRendererTests
{
    [Fact]
    public void Render_should_support_dotnet_date_and_numeric_formats()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 30, 14, 5, 9, TimeSpan.Zero);

        var value = NumberFormatRenderer.Render(
            "SO-{date:yyyyMMdd}-{date:HHmmss}-{sequence:D6}-{business}",
            "orders",
            occurredAt,
            42);

        Assert.Equal("SO-20260730-140509-000042-orders", value);
        Assert.Equal("20260730\u001F140509", NumberFormatRenderer.GetSequenceScope(
            "SO-{date:yyyyMMdd}-{date:HHmmss}-{sequence:D6}", occurredAt));
    }

    [Theory]
    [InlineData("SO-{date:yyyyMMdd}")]
    [InlineData("SO-{sequence:D4}-{sequence:D4}")]
    [InlineData("SO-{unknown}")]
    public void Render_should_reject_templates_without_one_supported_sequence(string format)
    {
        Assert.Throws<FormatException>(() => NumberFormatRenderer.Render(
            format,
            "orders",
            DateTimeOffset.UtcNow,
            1));
    }
}
