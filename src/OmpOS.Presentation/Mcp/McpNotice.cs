namespace OmpOS.Presentation.Mcp;

/// <summary>A configuration problem. It names files, servers and fields, and never a value.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="Origin">The file it belongs to.</param>
/// <param name="ServerName">The server it belongs to, or <see langword="null"/> for the whole file.</param>
/// <param name="Message">One sentence that says what is wrong and what to do.</param>
public sealed record McpNotice(McpNoticeSeverity Severity, string Origin, string? ServerName, string Message);
