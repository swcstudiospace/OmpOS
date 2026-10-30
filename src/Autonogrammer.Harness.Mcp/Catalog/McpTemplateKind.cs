namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>A skeleton the add-from-template action writes to the Autonogrammer-owned file.</summary>
public enum McpTemplateKind
{
    /// <summary>A server omp starts as a child process: <c>command</c> and <c>args</c>.</summary>
    Stdio = 0,

    /// <summary>A server reached over Streamable HTTP: <c>url</c> and an <c>Authorization</c> header that reads a variable.</summary>
    Http = 1,
}
