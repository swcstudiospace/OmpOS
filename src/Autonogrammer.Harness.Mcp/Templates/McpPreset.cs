using Autonogrammer.Harness.Mcp.Catalog;

namespace Autonogrammer.Harness.Mcp.Templates;

/// <summary>A ready-made MCP server entry for a well-known service. It is written to the Autonogrammer-owned <c>.autonogrammer/mcp.json</c> and stays switched off until the user enables it.</summary>
/// <param name="Id">The suggested server name: 1 to 64 letters, digits, <c>_</c>, <c>-</c> or <c>.</c>, lower case.</param>
/// <param name="DisplayName">The name to show.</param>
/// <param name="Description">One sentence that says what the server offers.</param>
/// <param name="Kind">Whether the server is a remote HTTP server or a child process.</param>
/// <param name="Status">Whether the address or command was confirmed in the vendor's documentation.</param>
/// <param name="Address">The Streamable HTTP address of a remote preset; <see langword="null"/> for a stdio preset.</param>
/// <param name="Command">The executable of a stdio preset; <see langword="null"/> for a remote preset.</param>
/// <param name="Args">The arguments of a stdio preset; empty for a remote preset. They may use <c>${workspaceFolder}</c>.</param>
/// <param name="Auth">How the server is signed in to.</param>
/// <param name="TokenVariable">The environment variable that holds the token when <paramref name="Auth"/> is <see cref="McpPresetAuth.BearerToken"/>; otherwise <see langword="null"/>.</param>
/// <param name="DocumentationAddress">The vendor page that states the address or command.</param>
/// <param name="VerifiedOn">The date (<c>yyyy-MM-dd</c>) on which the vendor page was read.</param>
/// <param name="Note">What the user needs to know before enabling the preset (a prerequisite, an alternative address), or <see langword="null"/>.</param>
public sealed record McpPreset(
    string Id,
    string DisplayName,
    string Description,
    McpTemplateKind Kind,
    McpPresetStatus Status,
    string? Address,
    string? Command,
    IReadOnlyList<string> Args,
    McpPresetAuth Auth,
    string? TokenVariable,
    string DocumentationAddress,
    string VerifiedOn,
    string? Note);
