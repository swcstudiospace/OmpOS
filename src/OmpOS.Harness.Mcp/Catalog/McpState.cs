namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>
/// The choices a user made about MCP servers, kept in an OmpOS-owned file: which servers are switched on or off, and which consent identities
/// are approved. A value is immutable; every change returns a new value. Consent is keyed by identity, not by name, so a server whose
/// declaration changed needs consent again.
/// </summary>
public sealed class McpState
{
    private readonly Dictionary<string, bool> _enabled;
    private readonly HashSet<string> _consented;

    private McpState(Dictionary<string, bool> enabled, HashSet<string> consented)
    {
        _enabled = enabled;
        _consented = consented;
    }

    /// <summary>Gets the state of a user who has chosen nothing: every server follows its file, and nothing is approved.</summary>
    public static McpState Empty { get; } = new(new Dictionary<string, bool>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Gets the servers the user switched on or off by name.</summary>
    public IReadOnlyDictionary<string, bool> EnabledOverrides => _enabled;

    /// <summary>Gets the consent identities the user approved.</summary>
    public IReadOnlySet<string> ConsentedIdentities => _consented;

    /// <summary>Returns whether the user switched a server on or off, or <see langword="null"/> when the server follows its file.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The user's choice, or null.</returns>
    public bool? EnabledOverride(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _enabled.TryGetValue(name, out var enabled) ? enabled : null;
    }

    /// <summary>Returns whether a consent identity is approved.</summary>
    /// <param name="identity">The identity from <see cref="Config.McpConsentIdentity"/>.</param>
    /// <returns>True when the user approved it.</returns>
    public bool IsConsented(string identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return _consented.Contains(identity);
    }

    /// <summary>Returns a state with a server switched on or off.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="enabled">True to switch the server on.</param>
    /// <returns>The new state.</returns>
    public McpState WithEnabled(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var copy = new Dictionary<string, bool>(_enabled, StringComparer.Ordinal) { [name] = enabled };
        return new McpState(copy, _consented);
    }

    /// <summary>Returns a state that approves a consent identity.</summary>
    /// <param name="identity">The identity to approve.</param>
    /// <returns>The new state.</returns>
    public McpState WithConsent(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var copy = new HashSet<string>(_consented, StringComparer.Ordinal) { identity };
        return new McpState(_enabled, copy);
    }

    /// <summary>Returns a state that no longer approves a consent identity.</summary>
    /// <param name="identity">The identity to withdraw.</param>
    /// <returns>The new state.</returns>
    public McpState WithoutConsent(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var copy = new HashSet<string>(_consented, StringComparer.Ordinal);
        _ = copy.Remove(identity);
        return new McpState(_enabled, copy);
    }

    /// <summary>Creates a state from persisted values.</summary>
    /// <param name="enabled">The enabled flags by name.</param>
    /// <param name="consented">The approved identities.</param>
    /// <returns>The state.</returns>
    public static McpState Create(IEnumerable<KeyValuePair<string, bool>> enabled, IEnumerable<string> consented)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(consented);
        return new McpState(
            new Dictionary<string, bool>(enabled, StringComparer.Ordinal),
            new HashSet<string>(consented, StringComparer.Ordinal));
    }
}
