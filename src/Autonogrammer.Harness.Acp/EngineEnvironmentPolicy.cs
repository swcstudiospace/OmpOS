using System.Collections.Frozen;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Decides which of the parent's environment variables a child process may inherit. <see cref="EngineEnvironmentMode.Inherit"/> keeps all of
/// them. <see cref="EngineEnvironmentMode.Allowlist"/> keeps only the variables an engine needs to run (<c>PATH</c>, <c>HOME</c>, <c>USER</c>,
/// <c>LOGNAME</c>, <c>LANG</c>, <c>LC_*</c>, <c>TERM</c>, <c>TZ</c>, <c>TMPDIR</c>, <c>SHELL</c>, <c>PI_CODING_AGENT_DIR</c>, <c>OMP_*</c>,
/// <c>NODE_EXTRA_CA_CERTS</c>, <c>SSL_CERT_FILE</c>, <c>SSL_CERT_DIR</c> and the proxy variables in upper and lower case) plus the entries of the
/// configured allow-list. An allow-list entry is an exact name, or a prefix when it ends in <c>*</c>. The caller applies its explicit variables
/// after the filter, so an explicit variable is always present and always wins. Names and values are never logged or placed in a message.
/// The allow-list is defined for POSIX hosts; names compare case-sensitively except on Windows.
/// </summary>
public sealed class EngineEnvironmentPolicy
{
    private const char PrefixMarker = '*';

    private static readonly string[] BuiltInNames =
    [
        "PATH",
        "HOME",
        "USER",
        "LOGNAME",
        "LANG",
        "TERM",
        "TZ",
        "TMPDIR",
        "SHELL",
        "PI_CODING_AGENT_DIR",
        "NODE_EXTRA_CA_CERTS",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "NO_PROXY",
        "http_proxy",
        "https_proxy",
        "no_proxy",
    ];

    private static readonly string[] BuiltInPrefixes = ["LC_", "OMP_"];

    private readonly FrozenSet<string> _names;
    private readonly string[] _prefixes;
    private readonly StringComparison _comparison;

    /// <summary>Initializes a new instance of the <see cref="EngineEnvironmentPolicy"/> class.</summary>
    /// <param name="mode">Whether the child inherits everything or only the allowed variables.</param>
    /// <param name="allowList">Extra exact names, or prefixes ending in <c>*</c>, kept in <see cref="EngineEnvironmentMode.Allowlist"/> mode. Null adds none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">An allow-list entry is blank, contains <c>=</c> or NUL, or uses <c>*</c> anywhere but at its end. The entry is not echoed.</exception>
    public EngineEnvironmentPolicy(EngineEnvironmentMode mode, IEnumerable<string>? allowList = null)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The environment mode is not a defined value.");
        }

        Mode = mode;
        _comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var names = new List<string>(BuiltInNames);
        var prefixes = new List<string>(BuiltInPrefixes);
        if (allowList is not null)
        {
            foreach (var entry in allowList)
            {
                if (!IsValidAllowEntry(entry))
                {
                    throw new ArgumentException("An environment allow-list entry is blank, contains '=' or NUL, or uses '*' anywhere but at its end.", nameof(allowList));
                }

                if (entry[^1] == PrefixMarker)
                {
                    prefixes.Add(entry[..^1]);
                }
                else
                {
                    names.Add(entry);
                }
            }
        }

        _names = names.ToFrozenSet(comparer);
        _prefixes = [.. prefixes];
    }

    /// <summary>Gets the policy that keeps the whole inherited environment.</summary>
    public static EngineEnvironmentPolicy Inherit { get; } = new(EngineEnvironmentMode.Inherit);

    /// <summary>Gets whether the child inherits everything or only the allowed variables.</summary>
    public EngineEnvironmentMode Mode { get; }

    /// <summary>Returns whether <paramref name="entry"/> is a usable allow-list entry: an exact name, or a prefix ending in <c>*</c>.</summary>
    /// <param name="entry">The configured entry.</param>
    /// <returns>True when the entry is non-blank, has no <c>=</c>, NUL or whitespace, and uses <c>*</c> only as its last character after at least one other character.</returns>
    public static bool IsValidAllowEntry(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        if (entry.Contains('=', StringComparison.Ordinal) || entry.Contains('\0', StringComparison.Ordinal) || entry.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var marker = entry.IndexOf(PrefixMarker, StringComparison.Ordinal);
        return marker < 0 || (marker == entry.Length - 1 && entry.Length > 1);
    }

    /// <summary>Returns whether a child may inherit the variable called <paramref name="name"/>.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True in <see cref="EngineEnvironmentMode.Inherit"/> mode, or when the name is built in or matches the allow-list.</returns>
    public bool IsAllowed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Mode == EngineEnvironmentMode.Inherit || _names.Contains(name))
        {
            return true;
        }

        foreach (var prefix in _prefixes)
        {
            if (name.StartsWith(prefix, _comparison))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the names among <paramref name="inheritedNames"/> that a child must not inherit. Empty in <see cref="EngineEnvironmentMode.Inherit"/> mode.</summary>
    /// <param name="inheritedNames">The variable names the child would inherit.</param>
    /// <returns>The names to remove, in the order given.</returns>
    public IReadOnlyList<string> Rejected(IEnumerable<string> inheritedNames)
    {
        ArgumentNullException.ThrowIfNull(inheritedNames);
        var rejected = new List<string>();
        foreach (var name in inheritedNames)
        {
            if (!IsAllowed(name))
            {
                rejected.Add(name);
            }
        }

        return rejected;
    }

    /// <summary>
    /// Brings a child's environment to this policy: removes every inherited variable the policy does not allow, then sets the explicit
    /// variables, which override an inherited variable of the same name.
    /// </summary>
    /// <param name="environment">The environment of the child, as <see cref="System.Diagnostics.ProcessStartInfo.Environment"/> holds it.</param>
    /// <param name="explicitVariables">The variables the caller sets deliberately. They are applied after the filter and are never removed.</param>
    public void Apply(IDictionary<string, string?> environment, IEnumerable<KeyValuePair<string, string>> explicitVariables)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(explicitVariables);
        foreach (var name in Rejected([.. environment.Keys]))
        {
            _ = environment.Remove(name);
        }

        foreach (var (name, value) in explicitVariables)
        {
            environment[name] = value;
        }
    }
}
