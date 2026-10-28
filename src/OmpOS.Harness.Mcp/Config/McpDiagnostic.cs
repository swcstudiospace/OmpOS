namespace OmpOS.Harness.Mcp.Config;

/// <summary>
/// A problem found while reading MCP configuration. A diagnostic names files, servers, fields and variable names, and never
/// carries an environment value, a header value, an argument or a URL: it is safe to log and to show.
/// </summary>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Origin">The file to show: relative to the workspace root for a project file, <c>~/</c> relative for a user file.</param>
/// <param name="ServerName">The server the problem belongs to, or <see langword="null"/> when it belongs to the whole file.</param>
/// <param name="Message">One plain sentence that says what is wrong and what to do.</param>
public sealed record McpDiagnostic(McpDiagnosticSeverity Severity, string Origin, string? ServerName, string Message);
