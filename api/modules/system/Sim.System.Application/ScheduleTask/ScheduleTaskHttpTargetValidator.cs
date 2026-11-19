using System.Net;
using System.Net.Sockets;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.ScheduleTask;

public static class ScheduleTaskHttpTargetValidator
{
    public static async Task<Uri> ValidateAsync(
        string? value,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new BusinessException(SystemErrorCodes.InvalidScheduleTaskConfiguration);
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            }
            catch (SocketException)
            {
                throw new BusinessException(SystemErrorCodes.HttpTargetNotAllowed);
            }
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivateOrReserved))
        {
            throw new BusinessException(SystemErrorCodes.HttpTargetNotAllowed);
        }

        return uri;
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateOrReserved(address.MapToIPv4());
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC;
        }

        var octets = address.GetAddressBytes();
        return octets[0] is 0 or 10 or 127 ||
               octets[0] >= 224 ||
               (octets[0] == 169 && octets[1] == 254) ||
               (octets[0] == 172 && octets[1] is >= 16 and <= 31) ||
               (octets[0] == 192 && octets[1] == 168) ||
               (octets[0] == 100 && octets[1] is >= 64 and <= 127) ||
               (octets[0] == 192 && octets[1] == 0 && octets[2] is 0 or 2) ||
               (octets[0] == 192 && octets[1] == 88 && octets[2] == 99) ||
               (octets[0] == 198 && octets[1] is 18 or 19) ||
               (octets[0] == 198 && octets[1] == 51 && octets[2] == 100) ||
               (octets[0] == 203 && octets[1] == 0 && octets[2] == 113);
    }
}
