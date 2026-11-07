using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Presentation.Settings.Connectors;

namespace Autonogrammer.Native.Composition.Connectors;

/// <summary>
/// Reads MCP permission tokens through <see cref="PermissionsLoader"/> and writes them with
/// <see cref="PermissionsFileEditor.SetPermissionTokens"/> to <c>~/.autonogrammer/permissions.json</c>.
/// </summary>
internal sealed class ConnectorPermissionStore : IConnectorPermissionStore
{
    private readonly string _home;
    private readonly string _path;

    /// <summary>Creates a store for one home directory.</summary>
    /// <param name="homeDirectory">The home directory. The user file is <c>.autonogrammer/permissions.json</c> under it.</param>
    public ConnectorPermissionStore(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        _home = homeDirectory;
        _path = Path.Combine(homeDirectory, ".autonogrammer", "permissions.json");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ReadAllowTokens() => Tokens(allow: true);

    /// <inheritdoc />
    public IReadOnlyList<string> ReadDenyTokens() => Tokens(allow: false);

    /// <inheritdoc />
    public void ReplaceAllowTokens(IReadOnlyList<string> tokens) => Write(PermissionsFileEditor.AllowTokensList, tokens);

    /// <inheritdoc />
    public void ReplaceDenyTokens(IReadOnlyList<string> tokens) => Write(PermissionsFileEditor.DenyTokensList, tokens);

    private IReadOnlyList<string> Tokens(bool allow)
    {
        var loaded = PermissionsLoader.Load(_home, projectRoot: null);
        var rules = allow ? loaded.AllowRules : loaded.DenyRules;
        return [.. rules.Where(rule => rule.Token.Kind == PermissionTokenKind.Mcp).Select(rule => rule.Token.Text)];
    }

    private void Write(string list, IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var result = PermissionsFileEditor.SetPermissionTokens(_path, list, tokens);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error ?? "The permission file could not be written.");
        }
    }
}
