namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>How serious a configuration problem is.</summary>
public enum McpDiagnosticSeverity
{
    /// <summary>Worth knowing; nothing is lost.</summary>
    Info = 0,

    /// <summary>Part of an entry is ignored or unsupported; the server is still usable.</summary>
    Warning = 1,

    /// <summary>A file or server entry cannot be used.</summary>
    Error = 2,
}
