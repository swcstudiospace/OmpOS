namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>The state of one configured server, in the order the catalog decides it.</summary>
public enum McpServerState
{
    /// <summary>The server is read and can be offered to a session. No connection check has run.</summary>
    Configured = 0,

    /// <summary>A project server that starts a process or reads the environment, and the user has not approved this exact declaration.</summary>
    NeedsConsent = 1,

    /// <summary>The server is switched off, in the file or by the user. It is not offered.</summary>
    Disabled = 2,

    /// <summary>A connection check is running.</summary>
    Connecting = 3,

    /// <summary>A connection check succeeded, or the tool registry holds the server's tools.</summary>
    Connected = 4,

    /// <summary>The server cannot be used: a variable is not set, or a connection check failed. <see cref="McpServerStatus.Reason"/> says why.</summary>
    Failed = 5,

    /// <summary>The server asked for sign-in. Run <c>/mcp reauth NAME</c> in a session: omp opens the browser on the omp host.</summary>
    AuthRequired = 6,
}
