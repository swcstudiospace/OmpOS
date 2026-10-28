using Autonogrammer.Harness.Mcp.Catalog;

namespace Autonogrammer.Harness.Mcp.Templates;

/// <summary>
/// The presets the MCP page offers next to the blank stdio and HTTP skeletons. Every preset in <see cref="Default"/> was read from the vendor's own documentation (the page is named in
/// <see cref="McpPreset.DocumentationAddress"/>); a preset that could not be confirmed is <see cref="McpPresetStatus.Unverified"/> and appears only in <see cref="All"/>. No preset holds a credential: a remote
/// server signs in through OAuth, or reads its token from a named environment variable.
/// </summary>
public static class McpTemplateCatalog
{
    private const string ReadOn = "2026-10-28";

    /// <summary>Gets every preset, including the ones that are not verified.</summary>
    public static IReadOnlyList<McpPreset> All { get; } =
    [
        Remote("linear", "Linear", "Issues, projects and comments in Linear.", "https://mcp.linear.app/mcp", McpPresetAuth.OAuth, null, "https://linear.app/docs/mcp", "Read-only access is available at https://mcp.linear.app/mcp/readonly."),
        Remote("notion", "Notion", "Search and edit Notion pages and databases.", "https://mcp.notion.com/mcp", McpPresetAuth.OAuth, null, "https://developers.notion.com/guides/mcp/get-started-with-mcp", null),
        Remote("sentry", "Sentry", "Look up Sentry issues, events and projects.", "https://mcp.sentry.dev/mcp", McpPresetAuth.OAuth, null, "https://docs.sentry.io/product/sentry-mcp/", "Add /<organization> or /<organization>/<project> to the address to scope the server."),
        Remote("github", "GitHub", "Repositories, issues, pull requests and Actions through GitHub's hosted server.", "https://api.githubcopilot.com/mcp/", McpPresetAuth.BearerToken, "GITHUB_PERSONAL_ACCESS_TOKEN", "https://github.com/github/github-mcp-server/blob/main/docs/remote-server.md", "Create a personal access token with only the permissions you need and export it as GITHUB_PERSONAL_ACCESS_TOKEN. Add /readonly to the address for read-only tools."),
        Remote("railway", "Railway", "Create projects, deploy templates, manage environments and variables on Railway.", "https://mcp.railway.com", McpPresetAuth.OAuth, null, "https://docs.railway.com/ai/mcp-server", "Railway also documents a CLI route: the railway mcp command (CLI 5.44.0 or later) reuses your railway login."),
        Remote("atlassian", "Atlassian", "Jira and Confluence through Atlassian's remote server.", "https://mcp.atlassian.com/v2/mcp", McpPresetAuth.OAuth, null, "https://support.atlassian.com/atlassian-rovo-mcp-server/docs/getting-started-with-the-atlassian-remote-mcp-server/", null),
        Remote("stripe", "Stripe", "Stripe objects and documentation through Stripe's hosted server.", "https://mcp.stripe.com", McpPresetAuth.OAuth, null, "https://docs.stripe.com/mcp", "Stripe also accepts an agent API key as a Bearer token; edit the entry to add an Authorization header that reads it from an environment variable."),
        Remote("supabase", "Supabase", "Manage Supabase projects, databases and functions.", "https://mcp.supabase.com/mcp", McpPresetAuth.OAuth, null, "https://supabase.com/docs/guides/getting-started/mcp", "Add ?read_only=true or ?project_ref=<id> to the address to limit the server; read the Supabase page before pointing it at production data."),
        Remote("vercel", "Vercel", "Vercel projects, deployments and logs.", "https://mcp.vercel.com", McpPresetAuth.OAuth, null, "https://vercel.com/docs/agent-resources/vercel-mcp", null),
        Remote("context7", "Context7", "Current library documentation and code examples.", "https://mcp.context7.com/mcp", McpPresetAuth.BearerToken, "CONTEXT7_API_KEY", "https://context7.com/docs/resources/all-clients", "Get an API key from the Context7 dashboard and export it as CONTEXT7_API_KEY. Use https://mcp.context7.com/mcp/oauth instead to sign in with OAuth."),
        Stdio("filesystem", "Filesystem", "Read and write files inside the workspace folder only.", "npx", ["-y", "@modelcontextprotocol/server-filesystem", "${workspaceFolder}"], "https://raw.githubusercontent.com/modelcontextprotocol/servers/main/src/filesystem/README.md", "Needs Node.js 24 or later. The server may use only the folders listed after its name; add more folders to the entry deliberately."),
        Stdio("fetch", "Fetch", "Fetch a web page and return it as markdown.", "uvx", ["mcp-server-fetch"], "https://raw.githubusercontent.com/modelcontextprotocol/servers/main/src/fetch/README.md", "Needs uv (uvx). The server makes network requests on your machine, so enable it only when you want the agent to read web pages."),
        Stdio("git", "Git", "Read, search and change a Git repository through the server's git tools.", "uvx", ["mcp-server-git", "--repository", "${workspaceFolder}"], "https://raw.githubusercontent.com/modelcontextprotocol/servers/main/src/git/README.md", "Needs uv (uvx). The vendor documents the server as able to manipulate the repository, not only read it."),
    ];

    /// <summary>Gets the presets the MCP page offers: <see cref="All"/> without the ones that are <see cref="McpPresetStatus.Unverified"/>.</summary>
    public static IReadOnlyList<McpPreset> Default { get; } = Verified(All);

    /// <summary>Keeps the presets whose address or command was confirmed against the vendor's documentation, in their order.</summary>
    /// <param name="presets">The presets to filter.</param>
    /// <returns>The presets that are <see cref="McpPresetStatus.Verified"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="presets"/> is null.</exception>
    public static IReadOnlyList<McpPreset> Verified(IEnumerable<McpPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);
        return [.. presets.Where(static preset => preset.Status == McpPresetStatus.Verified)];
    }

    /// <summary>Finds a verified preset by its id.</summary>
    /// <param name="id">The id, for example <c>linear</c>. The comparison is ordinal.</param>
    /// <returns>The preset, or <see langword="null"/> when no verified preset has the id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    public static McpPreset? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Default.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.Ordinal));
    }

    private static McpPreset Remote(string id, string name, string description, string url, McpPresetAuth auth, string? tokenVariable, string documentation, string? note) =>
        new(id, name, description, McpTemplateKind.Http, McpPresetStatus.Verified, url, null, [], auth, tokenVariable, documentation, ReadOn, note);

    private static McpPreset Stdio(string id, string name, string description, string command, string[] args, string documentation, string? note) =>
        new(id, name, description, McpTemplateKind.Stdio, McpPresetStatus.Verified, null, command, args, McpPresetAuth.None, null, documentation, ReadOn, note);
}
