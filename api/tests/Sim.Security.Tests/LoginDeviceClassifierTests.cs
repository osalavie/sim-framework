using Sim.System.Application.Security;
using Sim.System.Domain.Shared.Consts;

namespace Sim.Security.Tests;

public class LoginDeviceClassifierTests
{
    [Fact]
    public void ExplicitDeviceDescriptorIsNormalizedAndHashed()
    {
        var device = LoginDeviceClassifier.Classify(
            "device-123",
            "phone",
            "My phone",
            "test-agent",
            "127.0.0.1");

        Assert.Equal(LoginDeviceTypes.Phone, device.Type);
        Assert.Equal("My phone", device.Name);
        Assert.Equal(64, device.IdentifierHash.Length);
        Assert.DoesNotContain("device-123", device.IdentifierHash);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (iPad)", LoginDeviceTypes.Pad)]
    [InlineData("Mozilla/5.0 (iPhone) Mobile", LoginDeviceTypes.Phone)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) Chrome/120", LoginDeviceTypes.Pc)]
    public void UserAgentFallbackClassifiesSupportedDeviceTypes(
        string userAgent,
        string expectedType)
    {
        var device = LoginDeviceClassifier.Classify(
            null,
            null,
            null,
            userAgent,
            "127.0.0.1");

        Assert.Equal(expectedType, device.Type);
    }

    [Fact]
    public void LimitPolicyReplacesSameDeviceAndOldestExcessSession()
    {
        var sameDevice = Guid.NewGuid();
        var oldestOtherDevice = Guid.NewGuid();
        var phoneSession = Guid.NewGuid();
        var device = new LoginDeviceDescriptor("same-device", LoginDeviceTypes.Pc, "PC");
        var sessions = new[]
        {
            new ActiveLoginSession(
                sameDevice,
                "same-device",
                LoginDeviceTypes.Pc,
                DateTime.UtcNow),
            new ActiveLoginSession(
                oldestOtherDevice,
                "other-device",
                LoginDeviceTypes.Pc,
                DateTime.UtcNow.AddMinutes(-10)),
            new ActiveLoginSession(
                phoneSession,
                "phone",
                LoginDeviceTypes.Phone,
                DateTime.UtcNow.AddMinutes(-20))
        };

        var revoked = LoginSessionLimitPolicy.SelectSessionsToRevoke(sessions, device, 1);

        Assert.Contains(sameDevice, revoked);
        Assert.Contains(oldestOtherDevice, revoked);
        Assert.DoesNotContain(phoneSession, revoked);
    }
}
