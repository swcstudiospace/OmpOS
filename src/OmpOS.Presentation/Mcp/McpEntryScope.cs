namespace OmpOS.Presentation.Mcp;

/// <summary>Where the file that declares a server lives.</summary>
public enum McpEntryScope
{
    /// <summary>A file inside an open workspace.</summary>
    Project = 0,

    /// <summary>A file in the user's home directory.</summary>
    User = 1,
}
