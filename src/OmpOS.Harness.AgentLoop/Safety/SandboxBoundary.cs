namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// The read boundary at run time. With <see cref="SandboxReadBoundary.System"/> every resolvable path is readable. With
/// <see cref="SandboxReadBoundary.Workspace"/> a path is readable when its real path (links resolved) is inside the workspace, inside a
/// folder of the read allowlist, matches a glob of it, or is inside one of the skills, rules and plugins folders of <c>~/.ompos</c> and <c>~/.cursor</c>.
/// </summary>
internal sealed class SandboxBoundary
{
    private static readonly string[] HomeConventionFolders = [".ompos/skills", ".ompos/rules", ".ompos/plugins", ".cursor/skills", ".cursor/rules", ".cursor/plugins"];

    private readonly SafetyPaths _paths;
    private readonly List<string> _roots = [];
    private readonly List<string> _globs = [];

    /// <summary>Initializes a new instance of the <see cref="SandboxBoundary"/> class.</summary>
    /// <param name="sandbox">The sandbox in force.</param>
    /// <param name="paths">The path context.</param>
    public SandboxBoundary(EffectiveSandbox sandbox, SafetyPaths paths)
    {
        _paths = paths;
        ReadBoundary = sandbox.ReadBoundary;
        foreach (var entry in sandbox.AdditionalReadPaths)
        {
            var resolved = SafetyPatterns.Resolve(paths, entry);
            if (resolved.Real is null)
            {
                continue;
            }

            if (GlobMatcher.IsLiteral(resolved.Real))
            {
                _roots.Add(resolved.Real);
            }
            else
            {
                _globs.Add(resolved.Real);
            }
        }

        if (paths.Home is { } home)
        {
            foreach (var folder in HomeConventionFolders)
            {
                if (paths.Real(folder, home) is { } real)
                {
                    _roots.Add(real);
                }
            }
        }
    }

    /// <summary>Gets the boundary in force.</summary>
    public SandboxReadBoundary ReadBoundary { get; }

    /// <summary>Returns whether a path may be read without asking.</summary>
    /// <param name="real">The real path, or null when it cannot be resolved (never readable).</param>
    /// <returns>True when the path is inside the boundary.</returns>
    public bool IsReadable(string? real)
    {
        if (real is null)
        {
            return false;
        }

        if (ReadBoundary == SandboxReadBoundary.System || _paths.IsInWorkspace(real))
        {
            return true;
        }

        return _roots.Exists(root => _paths.IsUnder(real, root))
            || _globs.Exists(glob => GlobMatcher.MatchPath(glob, real, _paths.IgnoreCase));
    }
}
