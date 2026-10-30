namespace Autonogrammer.Presentation.Mcp;

/// <summary>The state of one MCP server as the settings page shows it. The harness catalog has the same states; a head maps one to the other.</summary>
public enum McpServerStatusKind
{
    /// <summary>Read and ready to be offered to a session.</summary>
    Configured = 0,

    /// <summary>A project server whose exact declaration the user has not approved.</summary>
    NeedsConsent = 1,

    /// <summary>Switched off.</summary>
    Disabled = 2,

    /// <summary>A connection check is running.</summary>
    Connecting = 3,

    /// <summary>A connection check succeeded.</summary>
    Connected = 4,

    /// <summary>The server cannot be used. The entry's reason says why.</summary>
    Failed = 5,

    /// <summary>The server asked for sign-in: run <c>/mcp reauth NAME</c> in a session.</summary>
    AuthRequired = 6,
}
