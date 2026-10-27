using Microsoft.Extensions.Options;
using OmpOS.Worktrees;

namespace OmpOS.Api.Services;

/// <summary>Resolves the workspace folder the API applies change decisions in and the label it shows for it.</summary>
internal sealed class WorkspaceLocator
{
    private readonly IOptions<SessionWorkspaceOptions> _workspace;
    private readonly IOptions<WorktreesOptions> _worktrees;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceLocator"/> class.</summary>
    /// <param name="workspace">The workspace options.</param>
    /// <param name="worktrees">The worktree options, whose repository names the workspace folder label when no workspace root is set.</param>
    public WorkspaceLocator(IOptions<SessionWorkspaceOptions> workspace, IOptions<WorktreesOptions> worktrees)
    {
        _workspace = workspace;
        _worktrees = worktrees;
    }

    /// <summary>Gets the absolute workspace root, or null when none is configured or the configured value is not an absolute path.</summary>
    public string? Root
    {
        get
        {
            var root = _workspace.Value.Root;
            return !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root)
                ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))
                : null;
        }
    }

    /// <summary>Gets the name of the workspace folder (never a full path), or null when there is none.</summary>
    public string? FolderLabel
    {
        get
        {
            var source = Root ?? (string.IsNullOrWhiteSpace(_worktrees.Value.Repository) ? null : _worktrees.Value.Repository);
            if (source is null)
            {
                return null;
            }

            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }
}
