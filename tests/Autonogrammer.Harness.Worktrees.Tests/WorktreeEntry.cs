namespace Autonogrammer.Worktrees.Tests;

/// <summary>
/// One entry of <c>git worktree list --porcelain</c>, used to assert what git reports.
/// </summary>
/// <param name="Path">The worktree path git reports.</param>
/// <param name="Branch">The full branch ref, for example <c>refs/heads/autonogrammer/run/a</c>, or null when detached.</param>
internal sealed record WorktreeEntry(string Path, string? Branch)
{
    /// <summary>
    /// Parses the porcelain output of <c>git worktree list</c>.
    /// </summary>
    /// <param name="porcelain">The porcelain output.</param>
    /// <returns>The entries in the order git lists them.</returns>
    public static IReadOnlyList<WorktreeEntry> ParsePorcelain(string porcelain)
    {
        var entries = new List<WorktreeEntry>();
        string? path = null;
        string? branch = null;

        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (path is not null)
                {
                    entries.Add(new WorktreeEntry(path, branch));
                }

                path = null;
                branch = null;
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..];
            }
        }

        if (path is not null)
        {
            entries.Add(new WorktreeEntry(path, branch));
        }

        return entries;
    }
}
