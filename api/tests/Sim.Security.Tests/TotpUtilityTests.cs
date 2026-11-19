using System.Text;
using Sim.System.Application.Security;

namespace Sim.Security.Tests;

public sealed class TotpUtilityTests
{
    [Fact]
    public void Compute_code_should_match_rfc_6238_sha1_vector_for_six_digits()
    {
        var secret = Encoding.ASCII.GetBytes("12345678901234567890");

        Assert.Equal("287082", TotpUtility.ComputeCode(secret, 1));
        Assert.True(TotpUtility.VerifyCode(
            secret,
            "287 082",
            DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime,
            allowedTimeStepDrift: 0));
    }

    [Fact]
    public void Base32_round_trip_should_preserve_secret()
    {
        var secret = TotpUtility.GenerateSecret();

        var encoded = TotpUtility.ToBase32(secret);

        Assert.Equal(secret, TotpUtility.FromBase32(encoded));
    }
}
