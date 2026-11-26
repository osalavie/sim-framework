using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Sim.Security.Tests;

[Collection(HostCollection.Name)]
public sealed class InputValidationTests(HostFixture fixture)
    : IClassFixture<HostFixture>
{
    [Fact]
    public async Task Login_with_missing_required_fields_returns_400()
    {
        using var client = fixture.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync("/api/system/user/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Sim:Framework:InvalidInput", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Malformed_json_login_returns_400()
    {
        using var client = fixture.CreateHttpsClient();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/system/user/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Sim:Framework:InvalidInput", await ReadErrorCodeAsync(response));
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("error")
            .GetProperty("code")
            .GetString();
    }
}
