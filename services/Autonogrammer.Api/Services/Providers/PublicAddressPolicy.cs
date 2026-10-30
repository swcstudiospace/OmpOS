using System.Net;
using System.Net.Sockets;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Classifies addresses for the two guards of the provider endpoints: a custom endpoint may never name a link-local or metadata address
/// (<see cref="IsLinkLocalOrMetadata"/>), and a live test only connects to a public address (<see cref="IsPublic"/>). IPv4 addresses
/// written inside an IPv6 address are unwrapped first, so <c>::ffff:169.254.169.254</c> is judged as the IPv4 address it is.
/// </summary>
internal static class PublicAddressPolicy
{
    /// <summary>Tells whether an address is link-local, a cloud metadata address, unspecified, multicast or reserved: never a valid endpoint.</summary>
    /// <param name="address">The address.</param>
    /// <returns>True when the address must be refused for every use.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public static bool IsLinkLocalOrMetadata(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var ip = Unwrap(address);
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 0
                || (b[0] == 169 && b[1] == 254)
                || b[0] >= 224
                || (b[0] == 100 && b[1] == 100 && b[2] == 100 && b[3] == 200)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0 && b[3] == 192)
                || (b[0] == 168 && b[1] == 63 && b[2] == 129 && b[3] == 16);
        }

        var bytes = ip.GetAddressBytes();
        return ip.Equals(IPAddress.IPv6None)
            || ip.Equals(IPAddress.IPv6Any)
            || ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal
            || ip.IsIPv6Multicast
            || (bytes[0] == 0xfd && bytes[1] == 0x00 && bytes[2] == 0x0e && bytes[3] == 0xc2 && bytes[15] == 0x54 && bytes[14] == 0x02);
    }

    /// <summary>Tells whether an address is on the public internet: not on this machine, not on a private or shared network and not reserved.</summary>
    /// <param name="address">The address.</param>
    /// <returns>True when a live test may connect to it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var ip = Unwrap(address);
        if (IPAddress.IsLoopback(ip) || IsLinkLocalOrMetadata(ip))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2)
                || (bytes[0] == 198 && bytes[1] is 18 or 19)
                || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113));
        }

        // fc00::/7 unique local; 2001::/32 Teredo, 2001:db8::/32 documentation, 2002::/16 6to4 and 64:ff9b::/96 NAT64 embed an IPv4 address and are refused.
        return !((bytes[0] & 0xfe) == 0xfc
            || (bytes[0] == 0x20 && bytes[1] == 0x01 && ((bytes[2] == 0x00 && bytes[3] == 0x00) || (bytes[2] == 0x0d && bytes[3] == 0xb8)))
            || (bytes[0] == 0x20 && bytes[1] == 0x02)
            || (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b));
    }

    private static IPAddress Unwrap(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
