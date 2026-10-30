using System.Globalization;

namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// Decides whether a <see cref="WorkspaceSet"/> can start a session. Errors: more than <see cref="MaxRoots"/> roots, a path that
/// is not absolute, a local folder that does not exist, a duplicate or nested root (compared by real path, so two links to one
/// folder are one root), roots in different environments (omp runs in one place, so a session cannot mix them), and names or
/// identifiers that repeat. Warning: a local root that is not a git working tree, which a run uses in place.
/// </summary>
public sealed class WorkspaceSetValidator
{
    /// <summary>The largest number of roots one set may hold.</summary>
    public const int MaxRoots = 16;

    private static readonly StringComparison LocalComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly IDirectoryProbe _probe;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSetValidator"/> class.
    /// </summary>
    /// <param name="probe">Answers existence, real-path and git questions for local roots.</param>
    public WorkspaceSetValidator(IDirectoryProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    /// <summary>
    /// Validates a set.
    /// </summary>
    /// <param name="set">The set to check.</param>
    /// <returns>Every finding; the set is usable when there is no error.</returns>
    public WorkspaceSetValidation Validate(WorkspaceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var issues = new List<WorkspaceSetIssue>();
        if (set.Roots.Count > MaxRoots)
        {
            issues.Add(Error(WorkspaceSetIssueCode.TooManyRoots, null, string.Create(CultureInfo.InvariantCulture, $"A workspace set holds at most {MaxRoots} roots; this one has {set.Roots.Count}. Remove some folders.")));
        }

        var canonical = new string?[set.Roots.Count];
        for (var index = 0; index < set.Roots.Count; index++)
        {
            canonical[index] = CheckRoot(set.Roots[index], issues);
        }

        CheckIdentity(set, issues);
        CheckEnvironments(set, issues);
        CheckOverlap(set, canonical, issues);
        return new WorkspaceSetValidation(issues);
    }

    private static WorkspaceSetIssue Error(WorkspaceSetIssueCode code, string? rootId, string message) =>
        new(WorkspaceSetIssueSeverity.Error, code, rootId, message);

    private static bool IsAbsolute(WorkspaceSetRoot root) =>
        root.Kind == WorkspaceRootKind.Local
            ? Path.IsPathRooted(root.Path)
            : root.Path.StartsWith('/') || (root.Path.Length >= 3 && char.IsAsciiLetter(root.Path[0]) && root.Path[1] == ':' && root.Path[2] is '/' or '\\');

    private static string TrimSeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        return path.Length > (root?.Length ?? 0) ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
    }

    private static string NormalizeRemote(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal) && segments.Count > 0)
            {
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (!string.Equals(segment, "..", StringComparison.Ordinal))
            {
                segments.Add(segment);
            }
        }

        return (path.StartsWith('/') ? "/" : string.Empty) + string.Join('/', segments);
    }

    private static bool IsInside(string parent, string child, WorkspaceRootKind kind)
    {
        var comparison = kind == WorkspaceRootKind.Local ? LocalComparison : StringComparison.Ordinal;
        var separator = kind == WorkspaceRootKind.Local ? Path.DirectorySeparatorChar : '/';
        var prefix = parent.EndsWith(separator) ? parent : parent + separator;
        return child.StartsWith(prefix, comparison) && child.Length > prefix.Length;
    }

    private string? CheckRoot(WorkspaceSetRoot root, List<WorkspaceSetIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(root.DisplayName))
        {
            issues.Add(Error(WorkspaceSetIssueCode.InvalidName, root.Id, $"A root of '{root.Path}' has no name. Give it one."));
        }

        if (string.IsNullOrWhiteSpace(root.Path) || root.Path.Contains('\0', StringComparison.Ordinal) || !IsAbsolute(root))
        {
            issues.Add(Error(WorkspaceSetIssueCode.InvalidPath, root.Id, $"'{root.DisplayName}': the folder path must be absolute and free of NUL characters."));
            return null;
        }

        if (root.Kind == WorkspaceRootKind.Remote)
        {
            return NormalizeRemote(root.Path);
        }

        var lexical = TrimSeparators(Path.GetFullPath(root.Path));
        if (!_probe.Exists(lexical))
        {
            issues.Add(Error(WorkspaceSetIssueCode.MissingDirectory, root.Id, $"'{root.DisplayName}': the folder '{root.Path}' does not exist."));
            return lexical;
        }

        if (!_probe.IsGitRoot(lexical))
        {
            issues.Add(new WorkspaceSetIssue(
                WorkspaceSetIssueSeverity.Warning,
                WorkspaceSetIssueCode.NotGitRepository,
                root.Id,
                $"'{root.DisplayName}' is not the top of a git repository, so a run uses it in place without a worktree; changes there are not isolated."));
        }

        return TrimSeparators(_probe.RealPath(lexical) ?? lexical);
    }

    private static void CheckIdentity(WorkspaceSet set, List<WorkspaceSetIssue> issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in set.Roots)
        {
            if (!ids.Add(root.Id))
            {
                issues.Add(Error(WorkspaceSetIssueCode.DuplicateId, root.Id, $"'{root.DisplayName}': the identifier '{root.Id}' is used by another root."));
            }

            if (!string.IsNullOrWhiteSpace(root.DisplayName) && !names.Add(WorkspaceRootNaming.Normalize(root.DisplayName)))
            {
                issues.Add(Error(WorkspaceSetIssueCode.InvalidName, root.Id, $"The name '{root.DisplayName}' is used by more than one root. Rename one."));
            }
        }
    }

    private static void CheckEnvironments(WorkspaceSet set, List<WorkspaceSetIssue> issues)
    {
        var first = set.Primary;
        var other = set.Roots.FirstOrDefault(root => !string.Equals(root.EnvironmentId, first.EnvironmentId, StringComparison.Ordinal));
        if (other is null)
        {
            return;
        }

        var environments = set.Roots.Select(root => root.EnvironmentId ?? "local").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        issues.Add(Error(
            WorkspaceSetIssueCode.MixedEnvironments,
            other.Id,
            $"Roots belong to different environments ({string.Join(", ", environments)}). omp runs in one place, so a session covers folders of one environment only."));
    }

    private static void CheckOverlap(WorkspaceSet set, string?[] canonical, List<WorkspaceSetIssue> issues)
    {
        for (var later = 1; later < set.Roots.Count; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                var a = set.Roots[earlier];
                var b = set.Roots[later];
                if (canonical[earlier] is not { } pathA
                    || canonical[later] is not { } pathB
                    || !string.Equals(a.EnvironmentId, b.EnvironmentId, StringComparison.Ordinal))
                {
                    continue;
                }

                var comparison = a.Kind == WorkspaceRootKind.Local ? LocalComparison : StringComparison.Ordinal;
                if (string.Equals(pathA, pathB, comparison))
                {
                    issues.Add(Error(WorkspaceSetIssueCode.DuplicateRoot, b.Id, $"'{b.DisplayName}' is the same folder as '{a.DisplayName}'. Keep one."));
                }
                else if (IsInside(pathA, pathB, a.Kind))
                {
                    issues.Add(Error(WorkspaceSetIssueCode.NestedRoot, b.Id, $"'{b.DisplayName}' is inside '{a.DisplayName}'. Roots must not overlap; remove one."));
                }
                else if (IsInside(pathB, pathA, a.Kind))
                {
                    issues.Add(Error(WorkspaceSetIssueCode.NestedRoot, a.Id, $"'{a.DisplayName}' is inside '{b.DisplayName}'. Roots must not overlap; remove one."));
                }
            }
        }
    }
}
