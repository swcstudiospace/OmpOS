using System.Globalization;

namespace OmpOS.Presentation.Mcp;

/// <summary>
/// Every text of the MCP page, in one place so the view carries no literal. No sentence holds an environment value, a header value or a URL path:
/// a server is shown by name, command as written, host and the names of its variables and headers.
/// </summary>
public static class McpCopy
{
    /// <summary>The navigation label of the MCP page.</summary>
    public const string Title = "MCP servers";

    /// <summary>The heading of the MCP page.</summary>
    public const string Heading = "MCP servers";

    /// <summary>The sentence under the heading.</summary>
    public const string Description =
        "Tool servers from your OmpOS, Cursor, Claude Code and VS Code configuration, offered to every session. "
        + "OmpOS reads those files and changes only .ompos/mcp.json.";

    /// <summary>The accessible name of the polite live region that announces the state.</summary>
    public const string StateAnnouncementName = "MCP servers state";

    /// <summary>The text while the configuration loads.</summary>
    public const string Loading = "Loading MCP server configuration.";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyHeading = "No MCP servers";

    /// <summary>The text of the empty state.</summary>
    public const string Empty =
        "No MCP configuration file declares a server. Add one to .ompos/mcp.json, .cursor/mcp.json, .mcp.json, .vscode/mcp.json, "
        + "~/.cursor/mcp.json or ~/.claude.json, or use Add from template below.";

    /// <summary>The label above the example.</summary>
    public const string ExampleLabel = "Example .ompos/mcp.json";

    /// <summary>The example shown in the empty state. It holds a variable name, never a value.</summary>
    public const string ExampleJson =
        "{\n"
        + "  \"mcpServers\": {\n"
        + "    \"docs\": { \"command\": \"npx\", \"args\": [\"-y\", \"some-mcp-server\"], \"env\": { \"API_KEY\": \"${env:DOCS_API_KEY}\" } },\n"
        + "    \"search\": { \"url\": \"https://mcp.example.com/mcp\", \"headers\": { \"Authorization\": \"Bearer ${env:SEARCH_TOKEN}\" } }\n"
        + "  }\n"
        + "}";

    /// <summary>The heading of the invalid-configuration state.</summary>
    public const string InvalidHeading = "MCP configuration has problems";

    /// <summary>The text of the invalid-configuration state.</summary>
    public const string Invalid = "No usable server was found. Fix the problems below, then reload.";

    /// <summary>The heading of the load-failure state.</summary>
    public const string ErrorHeading = "MCP servers could not be loaded";

    /// <summary>The heading of the list.</summary>
    public const string ListHeading = "MCP servers";

    /// <summary>The heading of a list in which something needs attention.</summary>
    public const string DegradedHeading = "MCP servers need attention";

    /// <summary>The label of the reload button.</summary>
    public const string ReloadLabel = "Reload";

    /// <summary>The accessible name of the reload button.</summary>
    public const string ReloadName = "Reload MCP configuration";

    /// <summary>The heading of the problems list.</summary>
    public const string ProblemsHeading = "Configuration problems";

    /// <summary>The accessible name of the problems list.</summary>
    public const string ProblemsName = "MCP configuration problems";

    /// <summary>The label of a switch-off button.</summary>
    public const string SwitchOffLabel = "Switch off";

    /// <summary>The label of a switch-on button.</summary>
    public const string SwitchOnLabel = "Switch on";

    /// <summary>The label of the button that opens the consent prompt.</summary>
    public const string ReviewLabel = "Review approval";

    /// <summary>The label of the button that withdraws an approval.</summary>
    public const string RevokeLabel = "Withdraw approval";

    /// <summary>The label of the connection-check button.</summary>
    public const string CheckLabel = "Check connection";

    /// <summary>The label of the button that opens a configuration file.</summary>
    public const string OpenLabel = "Open file";

    /// <summary>The heading of the consent prompt.</summary>
    public const string ConsentHeading = "Approve this server";

    /// <summary>The explanation in the consent prompt.</summary>
    public const string ConsentExplanation =
        "Approve records your consent for this exact declaration only. It does not start the server now: omp starts it in sessions that you open. "
        + "If the file changes the command, the arguments, the environment, the URL or the headers, you are asked again.";

    /// <summary>The label of the approve button.</summary>
    public const string ApproveLabel = "Approve";

    /// <summary>The label of the cancel button.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The accessible name of the approve button before a server is chosen.</summary>
    public const string ApproveName = "Approve server";

    /// <summary>The accessible name of the cancel button.</summary>
    public const string CancelName = "Cancel approval";

    /// <summary>The heading of the add-from-template section.</summary>
    public const string TemplateHeading = "Add from template";

    /// <summary>The explanation of the add-from-template section.</summary>
    public const string TemplateExplanation =
        "Writes a placeholder to .ompos/mcp.json, the one file OmpOS owns. It stays switched off until you edit it and switch it on.";

    /// <summary>The label of the stdio template button.</summary>
    public const string TemplateStdioLabel = "Add stdio server";

    /// <summary>The label of the http template button.</summary>
    public const string TemplateHttpLabel = "Add http server";

    /// <summary>The label of the template name field.</summary>
    public const string TemplateNameLabel = "New server name";

    /// <summary>The watermark of the template name field.</summary>
    public const string TemplateNameWatermark = "my-server";

    /// <summary>The sentence shown when no workspace is open and a template cannot be written.</summary>
    public const string TemplateNeedsWorkspace = "Open a workspace folder to add a server to its .ompos/mcp.json.";

    /// <summary>The accessible name of the action failure message.</summary>
    public const string ActionFailureName = "MCP action error";

    /// <summary>The prefix of the action failure message.</summary>
    public const string ActionFailurePrefix = "The last change failed. Reason: ";

    /// <summary>The OAuth note shown on a server that carries OAuth settings.</summary>
    public const string OAuthNote = "OAuth settings are not passed to omp. If the server asks for sign-in, run /mcp reauth in a session.";

    /// <summary>Returns the word for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A short word.</returns>
    public static string StatusWord(McpServerStatusKind status) => status switch
    {
        McpServerStatusKind.Configured => "Ready",
        McpServerStatusKind.NeedsConsent => "Needs approval",
        McpServerStatusKind.Disabled => "Off",
        McpServerStatusKind.Connecting => "Connecting",
        McpServerStatusKind.Connected => "Connected",
        McpServerStatusKind.Failed => "Failed",
        McpServerStatusKind.AuthRequired => "Sign-in needed",
        _ => string.Empty,
    };

    /// <summary>Returns the sentence that explains a status, or an empty string when the word says it all.</summary>
    /// <param name="status">The status.</param>
    /// <param name="name">The server name.</param>
    /// <param name="reason">The reason the source gave, or null.</param>
    /// <returns>One sentence or an empty string.</returns>
    public static string StatusDetail(McpServerStatusKind status, string name, string? reason) => status switch
    {
        McpServerStatusKind.NeedsConsent => "This project server is not offered to sessions until you approve it.",
        McpServerStatusKind.AuthRequired => "Sign-in needed. Run /mcp reauth " + name + " in the session; omp opens the browser on the machine it runs on.",
        McpServerStatusKind.Failed => string.IsNullOrWhiteSpace(reason) ? "The server is not available." : reason,
        McpServerStatusKind.Disabled => string.IsNullOrWhiteSpace(reason) ? "The server is not offered to sessions." : reason,
        _ => string.Empty,
    };

    /// <summary>Returns the line that says how many tools a server has, or an empty string when unknown.</summary>
    /// <param name="toolCount">The count, or null.</param>
    /// <returns>For example <c>3 tools</c>.</returns>
    public static string ToolCountText(int? toolCount) => toolCount switch
    {
        null => string.Empty,
        1 => "1 tool",
        { } count => count.ToString(CultureInfo.InvariantCulture) + " tools",
    };

    /// <summary>Returns the label of a list of names, or an empty string when there is none.</summary>
    /// <param name="label">The label, for example <c>Environment</c>.</param>
    /// <param name="names">The names.</param>
    /// <returns>For example <c>Environment: API_KEY, HOME</c>.</returns>
    public static string NamesText(string label, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(names);
        return names.Count == 0 ? string.Empty : label + ": " + string.Join(", ", names);
    }

    /// <summary>Returns the one-line summary of the list.</summary>
    /// <param name="servers">How many servers are listed.</param>
    /// <param name="attention">How many need attention.</param>
    /// <returns>A sentence.</returns>
    public static string Summary(int servers, int attention)
    {
        var listed = servers == 1 ? "1 server." : servers.ToString(CultureInfo.InvariantCulture) + " servers.";
        return attention == 0
            ? listed + " A server is offered to a session only when it is on, complete and, for a project server that starts a process, approved."
            : listed + " " + attention.ToString(CultureInfo.InvariantCulture) + " need attention and are not offered.";
    }
}
