using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace OmpOS.Web.Demo;

/// <summary>
/// The address rules of the custom provider route, the same table as the real endpoint: https only (plain http only for a host on this machine),
/// no user information, query or fragment, no whitespace or control character, no link-local, metadata, reserved or unspecified address, and an
/// IP address written in dotted decimal form.
/// </summary>
internal static partial class DemoProvidersEndpointCheck
{
    private const int MaxLength = 2048;

    private static readonly string[] MetadataNames =
    [
        "metadata",
        "metadata.google.internal",
        "metadata.goog",
        "instance-data",
        "instance-data.ec2.internal",
        "metadata.azure.internal",
        "metadata.platformequinix.com",
    ];

    /// <summary>Checks a base address.</summary>
    /// <param name="text">The text the caller sent.</param>
    /// <param name="problem">The sentence that says why the address is refused, or null.</param>
    /// <returns>The canonical address (no trailing slash), or null when it is refused.</returns>
    internal static string? Check(string? text, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "Enter the base URL of the endpoint.";
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxLength || trimmed.Any(static character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            problem = "The base URL is too long or holds a space or a control character.";
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            problem = "The base URL must be an http or https address.";
            return null;
        }

        var authority = RawAuthority(trimmed);
        if (uri.UserInfo.Length > 0 || authority.Contains('@', StringComparison.Ordinal) || authority.Contains('\\', StringComparison.Ordinal))
        {
            problem = "The base URL must not contain user information.";
            return null;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            problem = "The base URL must not contain a query or a fragment.";
            return null;
        }

        var host = uri.DnsSafeHost.TrimEnd('.');
        if (host.Length == 0 || host.Contains('%', StringComparison.Ordinal))
        {
            problem = "The base URL has no usable host.";
            return null;
        }

        problem = CheckHost(uri, host, authority, out var loopback);
        if (problem is null && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !loopback)
        {
            problem = "The base URL must use https; plain http is accepted only for a host on this machine.";
        }

        return problem is null ? uri.GetLeftPart(UriPartial.Path).TrimEnd('/') : null;
    }

    private static string? CheckHost(Uri uri, string host, string authority, out bool loopback)
    {
        loopback = false;
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            if (!IPAddress.TryParse(host, out var address))
            {
                return "The base URL has no usable host.";
            }

            if (uri.HostNameType == UriHostNameType.IPv4 && !string.Equals(RawHost(authority), host, StringComparison.OrdinalIgnoreCase))
            {
                return "Write an IP address in dotted decimal form, for example 203.0.113.7.";
            }

            if (IsLinkLocalOrMetadata(address))
            {
                return "The base URL names a link-local, metadata or reserved address, which is never a model endpoint.";
            }

            loopback = IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
            return null;
        }

        if (NumericHost().IsMatch(host))
        {
            return "Write an IP address in dotted decimal form, for example 203.0.113.7.";
        }

        if (MetadataNames.Contains(host, StringComparer.Ordinal) || host.EndsWith(".metadata.google.internal", StringComparison.Ordinal))
        {
            return "The base URL names a cloud metadata service, which is never a model endpoint.";
        }

        loopback = string.Equals(host, "localhost", StringComparison.Ordinal) || host.EndsWith(".localhost", StringComparison.Ordinal);
        return loopback || host.Contains('.', StringComparison.Ordinal) ? null : "The base URL host must be a full domain name.";
    }

    private static bool IsLinkLocalOrMetadata(IPAddress address)
    {
        var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 0
                || (b[0] == 169 && b[1] == 254)
                || b[0] >= 224
                || (b[0] == 100 && b[1] == 100 && b[2] == 100 && b[3] == 200)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0 && b[3] == 192);
        }

        return ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast;
    }

    private static string RawAuthority(string text)
    {
        var start = text.IndexOf("//", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var rest = text[(start + 2)..];
        var end = rest.IndexOfAny(['/', '?', '#']);
        return end < 0 ? rest : rest[..end];
    }

    private static string RawHost(string authority)
    {
        var colon = authority.LastIndexOf(':');
        return colon < 0 ? authority : authority[..colon];
    }

    [GeneratedRegex(@"^(?:0x[0-9a-f]+|[0-9]+)(?:\.(?:0x[0-9a-f]+|[0-9]+))*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NumericHost();
}
