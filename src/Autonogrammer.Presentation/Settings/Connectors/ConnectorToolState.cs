namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>The policy for one MCP server or tool.</summary>
public enum ConnectorToolState
{
    /// <summary>An allow rule. The tool runs without asking.</summary>
    AlwaysAllow = 0,

    /// <summary>No rule. The tool asks.</summary>
    NeedsApproval = 1,

    /// <summary>A deny rule. The tool is refused.</summary>
    Blocked = 2,
}
