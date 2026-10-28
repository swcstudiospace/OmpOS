namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>The size caps of MCP configuration. A file or entry over a cap is a diagnostic, never an exception.</summary>
public static class McpConfigLimits
{
    /// <summary>The largest configuration file that is read, in bytes. <c>~/.claude.json</c> holds more than servers, so the cap is generous.</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>The most servers read from one file; the rest are ignored with a warning.</summary>
    public const int MaxServersPerFile = 64;

    /// <summary>The longest server name.</summary>
    public const int MaxNameLength = 64;

    /// <summary>The longest command, in characters.</summary>
    public const int MaxCommandLength = 4096;

    /// <summary>The most arguments of one stdio server.</summary>
    public const int MaxArgs = 256;

    /// <summary>The longest argument, environment value, header value or URL, in characters.</summary>
    public const int MaxValueLength = 8192;

    /// <summary>The most environment variables or headers of one server.</summary>
    public const int MaxPairs = 128;

    /// <summary>The deepest JSON nesting that is read.</summary>
    public const int MaxJsonDepth = 32;

    /// <summary>The longest command preview shown for a server, in characters.</summary>
    public const int MaxPreviewLength = 512;
}
