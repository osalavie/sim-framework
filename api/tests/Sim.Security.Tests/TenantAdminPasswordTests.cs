using System.Reflection;
using System.Text.RegularExpressions;
using Sim.Admin.Domain.Services;

namespace Sim.Security.Tests;

public class TenantAdminPasswordTests
{
    [Fact]
    public void Generated_tenant_admin_password_is_strong_and_not_reused()
    {
        var method = typeof(TenantProvisioningService).GetMethod(
            "GenerateRandomPassword",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var first = (string)method.Invoke(null, null)!;
        var second = (string)method.Invoke(null, null)!;

        Assert.True(first.Length >= 32);
        Assert.NotEqual(first, second);
        Assert.Matches(
            new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$"),
            first);
    }
}
