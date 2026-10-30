using System.Collections.Frozen;
using System.Net;
using System.Text.RegularExpressions;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Checks the base URL of a custom endpoint before it is written to omp's configuration. The address must be https, except for a host on
/// this machine; user information, a query, a fragment, a link-local or metadata address (in any spelling .NET parses: decimal, octal,
/// hexadecimal, IPv4 inside IPv6) and the names cloud metadata services answer to are refused. An IPv4 address must be written in dotted
/// decimal, so the string omp reads is the address this check judged. The result is the canonical URL, never the text as typed.
/// </summary>
internal static partial class CustomEndpointValidator
{
    /// <summary>The longest base URL accepted.</summary>
    public const int MaxLength = 2048;

    private static readonly FrozenSet<string> MetadataNames = new[]
    {
        "metadata",
        "metadata.google.internal",
        "metadata.goog",
        "instance-data",
        "instance-data.ec2.internal",
        "metadata.azure.internal",
        "metadata.platformequinix.com",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Checks a base URL.</summary>
    /// <param name="text">The URL as the caller sent it.</param>
    /// <returns>The canonical URL, or the problem.</returns>
    public static CustomEndpointCheck Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Refuse("Enter the base URL of the endpoint.");
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxLength || trimmed.Any(static character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            return Refuse("The base URL is too long or holds a space or a control character.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            return Refuse("The base URL must be an http or https address.");
        }

        var authority = RawAuthority(trimmed);
        if (uri.UserInfo.Length > 0 || authority.Contains('@', StringComparison.Ordinal) || authority.Contains('\\', StringComparison.Ordinal))
        {
            return Refuse("The base URL must not contain user information.");
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return Refuse("The base URL must not contain a query or a fragment.");
        }

        var host = uri.DnsSafeHost.TrimEnd('.');
        if (host.Length == 0 || host.Contains('%', StringComparison.Ordinal))
        {
            return Refuse("The base URL has no usable host.");
        }

        var hostProblem = CheckHost(uri, host, authority, out var loopback);
        if (hostProblem is not null)
        {
            return Refuse(hostProblem);
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !loopback)
        {
            return Refuse("The base URL must use https; plain http is accepted only for a host on this machine.");
        }

        var canonical = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return new CustomEndpointCheck(new Uri(canonical, UriKind.Absolute), null);
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

            if (PublicAddressPolicy.IsLinkLocalOrMetadata(address))
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

        if (MetadataNames.Contains(host) || host.EndsWith(".metadata.google.internal", StringComparison.Ordinal))
        {
            return "The base URL names a cloud metadata service, which is never a model endpoint.";
        }

        loopback = string.Equals(host, "localhost", StringComparison.Ordinal) || host.EndsWith(".localhost", StringComparison.Ordinal);
        return loopback || host.Contains('.', StringComparison.Ordinal)
            ? null
            : "The base URL host must be a full domain name.";
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

    private static CustomEndpointCheck Refuse(string problem) => new(null, problem);

    [GeneratedRegex(@"^(?:0x[0-9a-f]+|[0-9]+)(?:\.(?:0x[0-9a-f]+|[0-9]+))*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NumericHost();
}
