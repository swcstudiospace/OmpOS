namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>Reads and writes the MCP permission tokens the matrix shows. Presentation does not know the file.</summary>
public interface IConnectorPermissionStore
{
    /// <summary>Reads the allow tokens.</summary>
    /// <returns>The allow tokens, as written.</returns>
    IReadOnlyList<string> ReadAllowTokens();

    /// <summary>Reads the deny tokens.</summary>
    /// <returns>The deny tokens, as written.</returns>
    IReadOnlyList<string> ReadDenyTokens();

    /// <summary>Replaces the allow tokens.</summary>
    /// <param name="tokens">The tokens to keep.</param>
    void ReplaceAllowTokens(IReadOnlyList<string> tokens);

    /// <summary>Replaces the deny tokens.</summary>
    /// <param name="tokens">The tokens to keep.</param>
    void ReplaceDenyTokens(IReadOnlyList<string> tokens);
}
