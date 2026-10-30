using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// The rules an <see cref="SshProfile"/> must obey before any of its values reaches an argument list or a remote shell line.
/// Every value that ends up in a process argument or in a command the remote shell parses is checked here first, so a hostile
/// alias, path or option cannot become an ssh option or a shell command.
/// </summary>
public static partial class SshProfileRules
{
    /// <summary>The default <see cref="SshProfile.KeepAliveSeconds"/>.</summary>
    public const int DefaultKeepAliveSeconds = 15;

    /// <summary>The smallest accepted <see cref="SshProfile.KeepAliveSeconds"/>.</summary>
    public const int MinKeepAliveSeconds = 5;

    /// <summary>The largest accepted <see cref="SshProfile.KeepAliveSeconds"/>.</summary>
    public const int MaxKeepAliveSeconds = 600;

    /// <summary>The longest accepted path, in characters.</summary>
    public const int MaxPathLength = 4096;

    private static readonly FrozenDictionary<string, Func<string, bool>> OptionRules =
        new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Compression"] = IsYesNo,
            ["ConnectionAttempts"] = value => IsIntegerInRange(value, 1, 10),
            ["ConnectTimeout"] = value => IsIntegerInRange(value, 1, 600),
            ["ServerAliveCountMax"] = value => IsIntegerInRange(value, 1, 20),
            ["AddressFamily"] = value => value is "any" or "inet" or "inet6",
            ["IdentitiesOnly"] = IsYesNo,
            ["StrictHostKeyChecking"] = value => value is "yes" or "ask",
            ["TCPKeepAlive"] = IsYesNo,
            ["PreferredAuthentications"] = IsAuthenticationList,
            ["UserKnownHostsFile"] = IsValidLocalPath,
            ["HostKeyAlias"] = IsValidHostname,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the ssh option names a profile may set. The comparison of names is case-insensitive.</summary>
    public static IReadOnlyCollection<string> AllowedOptionNames { get; } = OptionRules.Keys;

    /// <summary>Returns whether <paramref name="value"/> is a usable <see cref="EnvironmentProfile.Id"/>: 1 to 64 letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, starting with a letter or digit.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidProfileId(string? value) => value is not null && ProfileIdPattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable <see cref="SshProfile.Alias"/>: 1 to 128 letters, digits, <c>.</c>, <c>_</c>, <c>@</c> or <c>-</c>, not starting with <c>-</c> (an alias must never be read as an ssh option).</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidAlias(string? value) => value is not null && AliasPattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable <see cref="SshProfile.Hostname"/>: a DNS name, an IPv4 address or an unbracketed IPv6 address, not starting with <c>-</c>.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidHostname(string? value) => value is not null && HostnamePattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable <see cref="SshProfile.User"/>: 1 to 64 letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, not starting with <c>-</c>.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidUser(string? value) => value is not null && UserPattern().IsMatch(value);

    /// <summary>Returns whether <paramref name="value"/> is a usable <see cref="SshProfile.ProxyJump"/>: one to eight comma separated <c>[user@]host[:port]</c> hops, none starting with <c>-</c>.</summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidProxyJump(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 1024)
        {
            return false;
        }

        var hops = value.Split(',');
        if (hops.Length > 8)
        {
            return false;
        }

        foreach (var hop in hops)
        {
            if (!ProxyHopPattern().IsMatch(hop))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether <paramref name="port"/> is a TCP port.</summary>
    /// <param name="port">The candidate.</param>
    public static bool IsValidPort(int port) => port is >= 1 and <= 65535;

    /// <summary>
    /// Returns whether <paramref name="value"/> is an absolute POSIX path on the remote machine: it starts with <c>/</c>, holds no control
    /// character (no newline, no NUL, no tab) and no <c>..</c> segment, and is at most <see cref="MaxPathLength"/> characters.
    /// </summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidAbsoluteRemotePath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxPathLength || value[0] != '/' || HasControlCharacter(value))
        {
            return false;
        }

        foreach (var segment in value.Split('/'))
        {
            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is a usable <see cref="SshProfile.RemoteOmpPath"/>: an absolute remote path or a bare command
    /// name of 1 to 64 letters, digits, <c>.</c>, <c>_</c>, <c>+</c> or <c>-</c> that does not start with <c>-</c>.
    /// </summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidRemoteExecutable(string? value) =>
        value is not null && (IsValidAbsoluteRemotePath(value) || ExecutableNamePattern().IsMatch(value));

    /// <summary>
    /// Returns whether <paramref name="value"/> is a usable path on this machine (an identity file or a known-hosts file): not blank, at most
    /// <see cref="MaxPathLength"/> characters, no control character, not starting with <c>-</c>.
    /// </summary>
    /// <param name="value">The candidate.</param>
    public static bool IsValidLocalPath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxPathLength && value[0] != '-' && !HasControlCharacter(value);

    /// <summary>Returns whether an option name is in the allowlist, and gives its canonical spelling.</summary>
    /// <param name="name">The candidate option name, compared without regard to case.</param>
    /// <param name="canonical">The canonical spelling when the name is allowed; otherwise empty.</param>
    public static bool TryGetCanonicalOptionName(string? name, out string canonical)
    {
        canonical = string.Empty;
        if (name is null)
        {
            return false;
        }

        foreach (var known in OptionRules.Keys)
        {
            if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
            {
                canonical = known;
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an option is allowed with a value: the name is in the allowlist and the value passes the rule of that name.</summary>
    /// <param name="name">The option name, compared without regard to case.</param>
    /// <param name="value">The option value.</param>
    public static bool IsAllowedOption(string? name, string? value) =>
        name is not null && value is not null && OptionRules.TryGetValue(name, out var rule) && rule(value);

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsYesNo(string value) => value is "yes" or "no";

    private static bool IsIntegerInRange(string value, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum && number <= maximum;

    private static bool IsAuthenticationList(string value)
    {
        foreach (var method in value.Split(','))
        {
            if (method is not ("publickey" or "gssapi-with-mic" or "hostbased"))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProfileIdPattern();

    [GeneratedRegex("^[A-Za-z0-9._@][A-Za-z0-9._@-]{0,127}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AliasPattern();

    [GeneratedRegex("^[A-Za-z0-9._:][A-Za-z0-9._:-]{0,252}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HostnamePattern();

    [GeneratedRegex("^[A-Za-z0-9._][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserPattern();

    [GeneratedRegex("^(?:[A-Za-z0-9._][A-Za-z0-9._-]{0,63}@)?(?:[A-Za-z0-9._][A-Za-z0-9._-]{0,252}|\\[[0-9A-Fa-f:.]{2,45}\\])(?::[0-9]{1,5})?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProxyHopPattern();

    [GeneratedRegex("^[A-Za-z0-9._+][A-Za-z0-9._+-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExecutableNamePattern();
}
