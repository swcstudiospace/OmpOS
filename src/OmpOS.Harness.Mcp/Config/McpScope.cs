namespace OmpOS.Harness.Mcp.Config;

/// <summary>Where an MCP configuration file lives. A project file beats a user file.</summary>
public enum McpScope
{
    /// <summary>A file inside an open workspace root.</summary>
    Project = 0,

    /// <summary>A file in the user's home directory, shared by every workspace.</summary>
    User = 1,
}
