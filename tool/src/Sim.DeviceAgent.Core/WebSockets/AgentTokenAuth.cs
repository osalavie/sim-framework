using System.Security.Cryptography;
using System.Text;

namespace Sim.DeviceAgent.WebSockets;

public static class AgentTokenAuth
{
    public static bool TokenMatches(string? presented, string expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return true;
        }

        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes);
    }
}
