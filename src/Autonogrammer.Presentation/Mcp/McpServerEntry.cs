namespace Autonogrammer.Presentation.Mcp;

/// <summary>
/// One MCP server as an <see cref="IMcpSettingsSource"/> reports it. The record holds names and the command as written, never an environment
/// value, a header value or a URL path: a page that shows every field shows no secret.
/// </summary>
/// <param name="Name">The server name.</param>
/// <param name="Transport">The transport word shown: <c>stdio</c>, <c>http</c> or <c>sse</c>.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="Origin">The file that declares the server, to show.</param>
/// <param name="Status">The state.</param>
/// <param name="Reason">The one sentence that explains a failed or switched-off server, or <see langword="null"/>.</param>
/// <param name="ToolCount">How many tools the server advertised, when known.</param>
/// <param name="CommandPreview">The command line as written for a stdio server, or the scheme and host for a remote server.</param>
/// <param name="EnvNames">The environment variable names a stdio server receives.</param>
/// <param name="HeaderNames">The header names a remote server sends.</param>
/// <param name="IsEnabled">Whether the server is switched on.</param>
/// <param name="IsOffered">Whether the next session receives the server.</param>
/// <param name="RequiresConsent">Whether the server needs the user's approval.</param>
/// <param name="IsConsentGranted">Whether the user approved the server's exact declaration.</param>
/// <param name="HasOAuthSettings">Whether the entry carries OAuth settings, which are not passed to omp.</param>
public sealed record McpServerEntry(
    string Name,
    string Transport,
    McpEntryScope Scope,
    string Origin,
    McpServerStatusKind Status,
    string? Reason,
    int? ToolCount,
    string CommandPreview,
    IReadOnlyList<string> EnvNames,
    IReadOnlyList<string> HeaderNames,
    bool IsEnabled,
    bool IsOffered,
    bool RequiresConsent,
    bool IsConsentGranted,
    bool HasOAuthSettings);
