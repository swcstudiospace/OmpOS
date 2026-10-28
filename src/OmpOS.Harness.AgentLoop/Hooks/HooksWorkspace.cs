using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>The workspace a hook set is loaded for: its roots, whether it is remote, and how its files are read.</summary>
public sealed record HooksWorkspace
{
    /// <summary>Gets the absolute workspace roots, primary first. A multi-root workspace lists several.</summary>
    public required IReadOnlyList<string> Roots { get; init; }

    /// <summary>Gets a value indicating whether the workspace is remote or hosted. User scopes are then not read and <c>CURSOR_CODE_REMOTE</c> is set.</summary>
    public bool IsRemote { get; init; }

    /// <summary>
    /// Gets the file source for this workspace, or null to read the local file system. A remote or cloud environment injects the source that
    /// reaches its files.
    /// </summary>
    public IHookFileSource? Files { get; init; }

    /// <summary>
    /// Gets whether the project hooks of this workspace are trusted, overriding the user's trust list: true for an environment whose owner trusts
    /// its project hooks, false to withhold them, null to use the list of trusted workspaces.
    /// </summary>
    public bool? TrustProjectHooks { get; init; }
}
