namespace Autonogrammer.Presentation.Mcp;

/// <summary>A skeleton the add-from-template action writes to the Autonogrammer-owned file.</summary>
public enum McpTemplateChoice
{
    /// <summary>A server omp starts as a child process.</summary>
    Stdio = 0,

    /// <summary>A server reached over Streamable HTTP.</summary>
    Http = 1,
}
