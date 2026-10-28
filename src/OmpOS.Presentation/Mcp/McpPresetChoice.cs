namespace OmpOS.Presentation.Mcp;

/// <summary>A ready-made server of a well-known service that the page offers to add. It is the presentation view of a harness preset: a description and where to read about it, never a credential.</summary>
/// <param name="Id">The preset's id, which is also the server name it is added under, for example <c>linear</c>.</param>
/// <param name="DisplayName">The name to show, for example <c>Linear</c>.</param>
/// <param name="Description">One sentence that says what the server offers.</param>
/// <param name="Transport">Whether the server is a remote HTTP server or a child process.</param>
/// <param name="DocumentationAddress">The vendor page that states the server's address or command.</param>
/// <param name="AuthNote">One sentence that says how the server is signed in to; it never holds a credential.</param>
/// <param name="Note">What the user needs to know before switching the server on, or <see langword="null"/>.</param>
public sealed record McpPresetChoice(
    string Id,
    string DisplayName,
    string Description,
    McpTemplateChoice Transport,
    string DocumentationAddress,
    string AuthNote,
    string? Note);
