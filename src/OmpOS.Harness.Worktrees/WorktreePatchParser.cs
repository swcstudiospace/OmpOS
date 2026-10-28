using System.Globalization;

namespace OmpOS.Worktrees;

/// <summary>Reads the output of <c>git diff --numstat</c>, <c>git diff --name-status</c> and <c>git apply</c>.</summary>
internal static class WorktreePatchParser
{
    private const string PatchFailedPrefix = "error: patch failed: ";
    private const string ErrorPrefix = "error: ";

    /// <summary>Joins the numstat and name-status listings of one diff.</summary>
    /// <param name="numstat">The output of <c>git diff --numstat --no-renames</c>.</param>
    /// <param name="nameStatus">The output of <c>git diff --name-status --no-renames</c>.</param>
    /// <returns>One entry per file, in path order.</returns>
    public static IReadOnlyList<WorktreePatchFile> ParseFiles(string numstat, string nameStatus)
    {
        var statuses = new Dictionary<string, WorktreeFileStatus>(StringComparer.Ordinal);
        foreach (var line in Lines(nameStatus))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && parts[0].Length > 0)
            {
                statuses[parts[1]] = parts[0][0] switch
                {
                    'A' => WorktreeFileStatus.Added,
                    'M' => WorktreeFileStatus.Modified,
                    'D' => WorktreeFileStatus.Deleted,
                    _ => WorktreeFileStatus.Other,
                };
            }
        }

        var files = new List<WorktreePatchFile>();
        foreach (var line in Lines(numstat))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length != 3)
            {
                continue;
            }

            var binary = string.Equals(parts[0], "-", StringComparison.Ordinal) && string.Equals(parts[1], "-", StringComparison.Ordinal);
            _ = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var added);
            _ = int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var removed);
            files.Add(new WorktreePatchFile(parts[2], statuses.GetValueOrDefault(parts[2], WorktreeFileStatus.Modified), added, removed, binary));
        }

        return [.. files.OrderBy(static file => file.Path, StringComparer.Ordinal)];
    }

    /// <summary>Finds the files of a patch that <c>git apply</c> complained about.</summary>
    /// <param name="standardError">What <c>git apply</c> wrote to standard error.</param>
    /// <param name="files">The files of the patch. Only these are reported, so an unrelated error line never names a path.</param>
    /// <returns>The conflicting paths, in the order git named them, without repeats.</returns>
    public static IReadOnlyList<string> ConflictPaths(string standardError, IReadOnlyList<WorktreePatchFile> files)
    {
        var known = files.Select(static file => file.Path).ToHashSet(StringComparer.Ordinal);
        var found = new List<string>();
        foreach (var line in Lines(standardError))
        {
            var path = PathOf(line);
            if (path is not null && known.Contains(path) && !found.Contains(path, StringComparer.Ordinal))
            {
                found.Add(path);
            }
        }

        return found;
    }

    private static string? PathOf(string line)
    {
        if (line.StartsWith(PatchFailedPrefix, StringComparison.Ordinal))
        {
            var rest = line[PatchFailedPrefix.Length..];
            var colon = rest.LastIndexOf(':');
            return colon > 0 ? rest[..colon] : rest;
        }

        if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            var rest = line[ErrorPrefix.Length..];
            var separator = rest.IndexOf(": ", StringComparison.Ordinal);
            return separator > 0 ? rest[..separator] : null;
        }

        return null;
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(static line => line.TrimEnd('\r')).Where(static line => line.Length > 0);
}
