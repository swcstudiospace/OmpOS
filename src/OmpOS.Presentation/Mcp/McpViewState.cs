namespace OmpOS.Presentation.Mcp;

/// <summary>The one state the MCP page is in. A head shows exactly one state at a time.</summary>
public enum McpViewState
{
    /// <summary>The configuration is loading.</summary>
    Loading = 0,

    /// <summary>The configuration loaded and declares no server. The page shows an example.</summary>
    Empty = 1,

    /// <summary>The configuration could not be loaded. The page shows <see cref="McpSettingsViewModel.LoadFailure"/>.</summary>
    Error = 2,

    /// <summary>Every file with a problem is the only content: no usable server, at least one error. The page lists the problems.</summary>
    Invalid = 3,

    /// <summary>Servers are listed and none needs attention.</summary>
    Success = 4,

    /// <summary>Servers are listed and at least one needs approval, failed, needs sign-in, or a file has an error.</summary>
    Degraded = 5,
}
