using System.Globalization;

namespace OmpOS.Harness.Remote;

/// <summary>
/// The configuration ssh would use for one host: the output of <c>ssh -G &lt;alias&gt;</c>, which ssh produces after it has evaluated
/// <c>Host</c> and <c>Match</c> blocks, <c>Include</c> files and defaults. OmpOS never re-implements that grammar.
/// </summary>
public sealed record SshEffectiveConfig
{
    /// <summary>Gets the alias the configuration was resolved for.</summary>
    public required string Alias { get; init; }

    /// <summary>Gets every key ssh printed (lower case) with its values in output order. A key such as <c>identityfile</c> can appear several times.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Values { get; init; }

    /// <summary>Gets the host name ssh connects to.</summary>
    public string? Hostname => First("hostname");

    /// <summary>Gets the port ssh connects to.</summary>
    public int? Port => int.TryParse(First("port"), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : null;

    /// <summary>Gets the user name ssh logs in as.</summary>
    public string? User => First("user");

    /// <summary>Gets the identity files ssh would try, in order. Each may start with <c>~</c>.</summary>
    public IReadOnlyList<string> IdentityFiles => All("identityfile");

    /// <summary>Gets the jump hosts, or <see langword="null"/> when none is configured (ssh prints <c>none</c>).</summary>
    public string? ProxyJump => NotNone(First("proxyjump"));

    /// <summary>Gets the proxy command, or <see langword="null"/> when none is configured.</summary>
    public string? ProxyCommand => NotNone(First("proxycommand"));

    /// <summary>Gets whether the ssh agent connection is forwarded.</summary>
    public bool ForwardAgent => string.Equals(First("forwardagent"), "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the host key checking mode ssh would use, for example <c>ask</c> or <c>yes</c>.</summary>
    public string? StrictHostKeyChecking => First("stricthostkeychecking");

    /// <summary>Gets the known-hosts files ssh consults for the user, in order.</summary>
    public IReadOnlyList<string> UserKnownHostsFiles =>
        All("userknownhostsfile").SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();

    /// <summary>
    /// Parses the output of <c>ssh -G</c>: one <c>key value</c> pair per line, keys in lower case. Blank lines are ignored. A line with a key and
    /// no value is kept with an empty value.
    /// </summary>
    /// <param name="alias">The alias the output belongs to.</param>
    /// <param name="output">The standard output of <c>ssh -G</c>.</param>
    /// <returns>The parsed configuration.</returns>
    public static SshEffectiveConfig Parse(string alias, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentNullException.ThrowIfNull(output);
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOfAny([' ', '\t']);
            var key = (separator < 0 ? line : line[..separator]);
            var value = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
            if (!values.TryGetValue(key, out var list))
            {
                list = [];
                values[key] = list;
            }

            list.Add(value);
        }

        return new SshEffectiveConfig
        {
            Alias = alias,
            Values = values.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string? NotNone(string? value) =>
        string.IsNullOrEmpty(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? null : value;

    private string? First(string key) => Values.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : null;

    private IReadOnlyList<string> All(string key) => Values.TryGetValue(key, out var list) ? list : [];
}
