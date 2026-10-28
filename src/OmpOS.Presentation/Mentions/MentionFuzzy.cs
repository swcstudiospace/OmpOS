namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Fuzzy matching and ranking for mention names. A query matches when its characters occur in order in the name (a subsequence), compared without case.
/// The score prefers an exact name, then a prefix, then a substring, then a subsequence with bonuses for characters that start a word or follow each other;
/// a match in the file name outranks the same match in the folders of a path.
/// </summary>
public static class MentionFuzzy
{
    /// <summary>The most names <see cref="Rank{T}"/> scores. A longer list is cut so a huge tree cannot stall the picker.</summary>
    public const int MaxScanned = 50_000;

    /// <summary>Scores one name against a query.</summary>
    /// <param name="query">The query; an empty query scores 0 for every name.</param>
    /// <param name="name">The name or path.</param>
    /// <returns>A positive score for a match, 0 for an empty query, and -1 when the name does not match.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static double Score(string query, string name)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(name);

        if (query.Length == 0)
        {
            return 0;
        }

        var trimmed = name.Length > 1 && name[^1] == '/' ? name[..^1] : name;
        var slash = trimmed.LastIndexOf('/');
        var baseName = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        var inBase = ScoreOne(query, baseName);
        var inPath = slash >= 0 ? ScoreOne(query, trimmed) : inBase;
        if (inBase < 0 && inPath < 0)
        {
            return -1;
        }

        // The file name counts one and a half times: typing "app" should find App.cs before src/apple/readme.md.
        return Math.Max(inBase < 0 ? -1 : inBase * 1.5, inPath) - Math.Min(name.Length * 0.01, 0.9);
    }

    /// <summary>Ranks items by their name against a query and keeps the best.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="query">The query. An empty query keeps the first items in their given order.</param>
    /// <param name="name">Reads an item's name or path.</param>
    /// <param name="limit">The most items to keep.</param>
    /// <returns>The items with their scores, best first; equal scores keep the name order (ordinal).</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<(T Item, double Score)> Rank<T>(IEnumerable<T> items, string query, Func<T, string> name, int limit)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(name);

        var scored = new List<(T Item, double Score, string Name)>();
        var scanned = 0;
        foreach (var item in items)
        {
            if (++scanned > MaxScanned)
            {
                break;
            }

            var text = name(item);
            if (query.Length == 0)
            {
                scored.Add((item, 0, text));
                if (scored.Count >= limit)
                {
                    break;
                }

                continue;
            }

            var score = Score(query, text);
            if (score >= 0)
            {
                scored.Add((item, score, text));
            }
        }

        if (query.Length > 0)
        {
            scored.Sort(static (left, right) =>
            {
                var byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : string.CompareOrdinal(left.Name, right.Name);
            });
        }

        return scored.Take(Math.Max(limit, 0)).Select(static entry => (entry.Item, entry.Score)).ToList();
    }

    private static double ScoreOne(string query, string text)
    {
        if (text.Length == 0)
        {
            return -1;
        }

        if (string.Equals(text, query, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 800 - Math.Min(text.Length - query.Length, 100);
        }

        var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            var boundary = at == 0 || !char.IsLetterOrDigit(text[at - 1]) ? 100 : 0;
            return 500 + boundary - Math.Min(at, 100);
        }

        return SubsequenceScore(query, text);
    }

    private static double SubsequenceScore(string query, string text)
    {
        var score = 100.0;
        var position = 0;
        var previous = -2;
        foreach (var wanted in query)
        {
            var found = -1;
            for (var index = position; index < text.Length; index++)
            {
                if (char.ToUpperInvariant(text[index]) == char.ToUpperInvariant(wanted))
                {
                    found = index;
                    break;
                }
            }

            if (found < 0)
            {
                return -1;
            }

            if (found == previous + 1)
            {
                score += 15;
            }

            if (found == 0 || !char.IsLetterOrDigit(text[found - 1]) || (char.IsUpper(text[found]) && char.IsLower(text[found - 1])))
            {
                score += 25;
            }

            score -= Math.Min(found - position, 10);
            previous = found;
            position = found + 1;
        }

        return Math.Max(score, 1);
    }
}
