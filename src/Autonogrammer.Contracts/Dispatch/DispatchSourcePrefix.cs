using System.Net;
using System.Net.Sockets;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// The network prefix a device connection is recorded under (ADR-0036, D-12). An IPv4 address is kept as its /24 and an IPv6 address as its /48,
/// so the device row shows where a device connects from without keeping the address itself. An IPv4-mapped IPv6 address is read as IPv4.
/// </summary>
public static class DispatchSourcePrefix
{
    /// <summary>Gets the prefix of a remote address.</summary>
    /// <param name="address">The remote address, or null when the request has none.</param>
    /// <returns>The prefix in CIDR form, or null when there is no address or its family is neither IPv4 nor IPv6.</returns>
    public static string? Of(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var resolved = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (resolved.AddressFamily == AddressFamily.InterNetwork)
        {
            var octets = resolved.GetAddressBytes();
            return $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
        }

        if (resolved.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = new byte[16];
            Array.Copy(resolved.GetAddressBytes(), bytes, 6);
            return new IPAddress(bytes).ToString() + "/48";
        }

        return null;
    }
}
