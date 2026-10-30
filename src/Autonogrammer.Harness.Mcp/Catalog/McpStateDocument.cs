namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>The persisted form of <see cref="McpState"/>. <see cref="Version"/> is bumped when the shape changes; a file with another version is treated as empty.</summary>
internal sealed class McpStateDocument
{
    /// <summary>Gets or sets the document version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the enabled flags by server name.</summary>
    public Dictionary<string, bool> Enabled { get; set; } = [];

    /// <summary>Gets or sets the approved consent identities.</summary>
    public List<string> Consented { get; set; } = [];
}
