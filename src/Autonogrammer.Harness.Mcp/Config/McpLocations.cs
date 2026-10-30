namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>
/// Lists the configuration files in precedence order. A project file beats a user file, and an earlier workspace root beats a
/// later one. Inside a root the order is <c>.autonogrammer/mcp.json</c>, <c>.cursor/mcp.json</c>, <c>.mcp.json</c>, <c>.vscode/mcp.json</c>;
/// inside the home it is <c>~/.autonogrammer/mcp.json</c>, <c>~/.cursor/mcp.json</c>, <c>~/.claude.json</c>.
/// </summary>
public static class McpLocations
{
    private const int UserPriorityBase = 10_000;
    private const int RootPriorityStep = 10;

    /// <summary>Gets the path of the Autonogrammer-owned project file under a workspace root. It is the only file Autonogrammer writes.</summary>
    /// <param name="workspaceRoot">The workspace root.</param>
    /// <returns>The path of <c>.autonogrammer/mcp.json</c>.</returns>
    public static string OwnedProjectFile(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return Path.Combine(workspaceRoot, ".autonogrammer", "mcp.json");
    }

    /// <summary>Lists every location in precedence order, whether or not a file exists there.</summary>
    /// <param name="context">The roots and home to search.</param>
    /// <returns>The locations, highest precedence first.</returns>
    public static IReadOnlyList<McpLocation> Enumerate(McpLocatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var locations = new List<McpLocation>();

        for (var index = 0; index < context.WorkspaceRoots.Count; index++)
        {
            var root = context.WorkspaceRoots[index];
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var prefix = context.WorkspaceRoots.Count == 1 ? string.Empty : RootName(root) + "/";
            var step = index * RootPriorityStep;
            locations.Add(Project(root, prefix, ".autonogrammer/mcp.json", McpConfigFormat.Autonogrammer, step));
            locations.Add(Project(root, prefix, ".cursor/mcp.json", McpConfigFormat.Cursor, step + 1));
            locations.Add(Project(root, prefix, ".mcp.json", McpConfigFormat.ClaudeProject, step + 2));
            locations.Add(Project(root, prefix, ".vscode/mcp.json", McpConfigFormat.ServersObject, step + 3));
        }

        if (!string.IsNullOrWhiteSpace(context.UserHome))
        {
            locations.Add(User(context.UserHome, ".autonogrammer/mcp.json", McpConfigFormat.Autonogrammer, UserPriorityBase));
            locations.Add(User(context.UserHome, ".cursor/mcp.json", McpConfigFormat.Cursor, UserPriorityBase + 1));
            locations.Add(User(context.UserHome, ".claude.json", McpConfigFormat.ClaudeUser, UserPriorityBase + 2));
        }

        return locations;
    }

    private static McpLocation Project(string root, string prefix, string relative, McpConfigFormat format, int priority) =>
        new(McpScope.Project, format, Path.Combine(root, relative), prefix + relative, root, priority);

    private static McpLocation User(string home, string relative, McpConfigFormat format, int priority) =>
        new(McpScope.User, format, Path.Combine(home, relative), "~/" + relative, null, priority);

    private static string RootName(string root)
    {
        var trimmed = root.TrimEnd('/', '\\');
        var name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }
}
