namespace OmpOS.Presentation.Mentions;

/// <summary>The search over a workspace tree that the file and folder sources share.</summary>
internal static class MentionFileCandidates
{
    /// <summary>Searches the tree for files or folders.</summary>
    /// <param name="entries">The tree.</param>
    /// <param name="ignore">The ignore filter, or null for none.</param>
    /// <param name="query">The typed name. A trailing slash lists the children of that folder.</param>
    /// <param name="limit">The most rows.</param>
    /// <param name="directories">Whether to return folders (true) or files (false).</param>
    /// <returns>The rows, best first, and whether the list was cut at the limit.</returns>
    internal static MentionSearchResult Search(IReadOnlyList<MentionFileEntry> entries, IMentionIgnoreFilter? ignore, string query, int limit, bool directories)
    {
        var kind = directories ? MentionKind.Folder : MentionKind.File;
        var normalized = query.Replace('\\', '/');
        var folderQuery = normalized.EndsWith('/');
        var parent = folderQuery ? normalized.TrimEnd('/') : null;
        var exactSecret = !directories && normalized.Length >= 2;

        var pool = new List<MentionFileEntry>();
        foreach (var entry in entries)
        {
            if (entry.IsDirectory != directories || (ignore is not null && ignore.IsIgnored(entry.Path, entry.IsDirectory)))
            {
                continue;
            }

            if (parent is not null && !string.Equals(ParentOf(entry.Path), parent, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!directories && MentionSecretFiles.IsSecretBearing(entry.Path) && !(exactSecret && IsExactName(entry.Path, normalized)))
            {
                continue;
            }

            pool.Add(entry);
        }

        IEnumerable<MentionFileEntry> source = pool;
        var scoreQuery = folderQuery ? string.Empty : normalized;
        if (scoreQuery.Length == 0)
        {
            source = pool.OrderBy(static entry => Depth(entry.Path)).ThenBy(static entry => entry.Path, StringComparer.Ordinal);
        }

        var ranked = MentionFuzzy.Rank(source, scoreQuery, static entry => entry.Path, limit + 1);
        var hasMore = ranked.Count > limit;
        var items = ranked
            .Take(limit)
            .Select(pair => ToCandidate(pair.Item, kind, pair.Score, exactSecret && IsExactName(pair.Item.Path, normalized)))
            .ToList();
        return new MentionSearchResult(items, null, hasMore);
    }

    private static MentionCandidate ToCandidate(MentionFileEntry entry, MentionKind kind, double score, bool confirm)
    {
        var secret = kind == MentionKind.File && MentionSecretFiles.IsSecretBearing(entry.Path) && confirm;
        var parent = ParentOf(entry.Path);
        var display = kind == MentionKind.Folder ? entry.Path + "/" : entry.Path;
        return new MentionCandidate(
            kind,
            entry.Path,
            display,
            parent.Length == 0 ? null : parent,
            score,
            RequiresConfirmation: secret,
            SizeBytes: kind == MentionKind.File ? entry.SizeBytes : -1);
    }

    private static bool IsExactName(string path, string typed)
    {
        var name = typed.TrimStart('/');
        if (string.Equals(path, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var slash = path.LastIndexOf('/');
        return string.Equals(slash >= 0 ? path[(slash + 1)..] : path, name, StringComparison.OrdinalIgnoreCase);
    }

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    private static int Depth(string path) => path.Count(static character => character == '/');
}
