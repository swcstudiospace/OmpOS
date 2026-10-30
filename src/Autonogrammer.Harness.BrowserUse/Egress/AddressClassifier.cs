using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>
/// Classifies addresses before a dial. Literals are normalised, mapped and NAT64 forms are unwrapped and
/// re-checked, and a literal <see cref="IPAddress.TryParse(string, out IPAddress?)"/> can read in more than one
/// way is refused. This type does not call DNS.
/// </summary>
public static class AddressClassifier
{
    private static readonly byte[] Nat64Prefix = [0x00, 0x64, 0xff, 0x9b, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>Classifies a parsed address, unwrapping mapped and NAT64 forms first.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The classification.</returns>
    public static AddressClassification Classify(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            return DenyIfInner(Classify(address.MapToIPv4()), "mapped");
        }

        if (TryUnwrapNat64(address, out var nat64))
        {
            return DenyIfInner(Classify(nat64), "nat64");
        }

        if (TryUnwrapCompatible(address, out var compatible))
        {
            return DenyIfInner(Classify(compatible), "compatible");
        }

        return address.AddressFamily == AddressFamily.InterNetwork
            ? ClassifyV4(address)
            : ClassifyV6(address);
    }

    /// <summary>Normalises a literal and classifies it. Ambiguous and unparsed literals are refused.</summary>
    /// <param name="literal">The address literal.</param>
    /// <returns>The classification.</returns>
    public static AddressClassification ClassifyLiteral(string literal)
    {
        ArgumentException.ThrowIfNullOrEmpty(literal);
        if (IsAmbiguous(literal))
        {
            return new AddressClassification(AddressClass.Ambiguous, "ambiguous");
        }

        if (!IPAddress.TryParse(literal, out var address))
        {
            return new AddressClassification(AddressClass.Denied, "unparsed");
        }

        return Classify(address);
    }

    private static AddressClassification DenyIfInner(AddressClassification inner, string wrapper)
    {
        if (!inner.IsRefused)
        {
            return inner;
        }

        return new AddressClassification(inner.Class, wrapper + ":" + inner.Reason);
    }

    private static bool IsAmbiguous(string literal)
    {
        var colon = literal.LastIndexOf(':');
        if (colon >= 0)
        {
            var tail = literal[(colon + 1)..];
            return tail.Contains('.', StringComparison.Ordinal) && IsAmbiguous(tail);
        }

        var parts = literal.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!TryComponent(part, out var primary, out var secondary))
            {
                return false;
            }

            if (secondary is not null && secondary.Value != primary)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryComponent(string part, out uint primary, out uint? secondary)
    {
        primary = 0;
        secondary = null;
        if (part.Length == 0)
        {
            return false;
        }

        if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(part[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out primary);
        }

        if (!uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return false;
        }

        primary = decimalValue;
        if (part.Length > 1 && part[0] == '0' && part.All(static digit => digit is >= '0' and <= '7'))
        {
            primary = Convert.ToUInt32(part, 8);
            secondary = decimalValue;
        }

        return true;
    }

    private static AddressClassification ClassifyV4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        var a = bytes[0];
        var b = bytes[1];
        var c = bytes[2];
        if (a == 0)
        {
            return Denied("unspecified");
        }

        if (a == 10 || (a == 172 && b is >= 16 and <= 31) || (a == 192 && b == 168))
        {
            return Denied("rfc1918");
        }

        if (a == 127)
        {
            return Denied("loopback");
        }

        if (a == 169 && b == 254)
        {
            return Denied("link-local");
        }

        if (a == 100 && b is >= 64 and <= 127)
        {
            return Denied("cgnat");
        }

        if (a is >= 224 and <= 239)
        {
            return Denied("multicast");
        }

        if (a >= 240
            || (a == 192 && b == 0 && c == 0)
            || (a == 192 && b == 0 && c == 2)
            || (a == 198 && b is 18 or 19)
            || (a == 198 && b == 51 && c == 100)
            || (a == 203 && b == 0 && c == 113))
        {
            return Denied("reserved");
        }

        return new AddressClassification(AddressClass.Allowed, "public");
    }

    private static AddressClassification ClassifyV6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any))
        {
            return Denied(address.Equals(IPAddress.IPv6Any) ? "unspecified" : "loopback");
        }

        var bytes = address.GetAddressBytes();
        if (bytes[0] == 0xFF)
        {
            return Denied("multicast");
        }

        if ((bytes[0] & 0xFE) == 0xFC)
        {
            return Denied("unique-local");
        }

        if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
        {
            return Denied("link-local");
        }

        if ((bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0xC0)
            || (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8))
        {
            return Denied("reserved");
        }

        return new AddressClassification(AddressClass.Allowed, "public");
    }

    private static bool TryUnwrapNat64(IPAddress address, out IPAddress inner)
    {
        inner = IPAddress.None;
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (!bytes.AsSpan(0, Nat64Prefix.Length).SequenceEqual(Nat64Prefix))
        {
            return false;
        }

        inner = new IPAddress(bytes.AsSpan(12, 4));
        return true;
    }

    private static bool TryUnwrapCompatible(IPAddress address, out IPAddress inner)
    {
        inner = IPAddress.None;
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.AsSpan(0, 12).ContainsAnyExcept((byte)0))
        {
            return false;
        }

        if (bytes.AsSpan(12, 4).SequenceEqual(new byte[] { 0, 0, 0, 0 })
            || bytes.AsSpan(12, 4).SequenceEqual(new byte[] { 0, 0, 0, 1 }))
        {
            return false;
        }

        inner = new IPAddress(bytes.AsSpan(12, 4));
        return true;
    }

    private static AddressClassification Denied(string reason) => new(AddressClass.Denied, reason);
}
