namespace Autonogrammer.Presentation.Mcp;

/// <summary>How serious a configuration problem is.</summary>
public enum McpNoticeSeverity
{
    /// <summary>Worth knowing; nothing is lost.</summary>
    Info = 0,

    /// <summary>Part of an entry is ignored; the server is still usable.</summary>
    Warning = 1,

    /// <summary>A file or entry cannot be used.</summary>
    Error = 2,
}
