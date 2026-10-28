namespace Autonogrammer.Harness.Mcp.Templates;

/// <summary>How a preset's server is signed in to.</summary>
public enum McpPresetAuth
{
    /// <summary>The server needs no credential, or a stdio server holds its own.</summary>
    None = 0,

    /// <summary>The client signs in through the vendor's OAuth flow in a browser; the file holds no credential. omp starts the flow (<c>/mcp reauth</c>).</summary>
    OAuth = 1,

    /// <summary>The vendor documents a token sent as <c>Authorization: Bearer</c>. The file names an environment variable (<see cref="McpPreset.TokenVariable"/>) and never holds the token.</summary>
    BearerToken = 2,
}
