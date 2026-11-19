using System.Text.Json;
using Sim.System.Application.Contracts.User;

namespace Sim.Security.Tests;

public class LoginUserDtoTests
{
    [Fact]
    public void LoginResponseDoesNotDefineRefreshToken()
    {
        var json = JsonSerializer.Serialize(new LoginUserDto
        {
            AccessToken = "access-token"
        });

        Assert.Null(typeof(LoginUserDto).GetProperty("RefreshToken"));
        Assert.Contains("accessToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
    }
}
