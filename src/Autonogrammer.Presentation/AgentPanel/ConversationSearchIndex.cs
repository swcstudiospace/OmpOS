namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// A bounded, in-memory full-text index over conversation titles and transcript text. The host feeds it the open sessions (<see cref="Upsert"/>) as their transcripts change.
/// It is bounded three ways (<see cref="ConversationSearchOptions"/>): sessions, characters per session and characters in all; past a bound the oldest data goes first, so
/// a long running application never grows without limit. A query is split into words; a session matches when every word is in its title or its text, compared without case.
/// </summary>
public sealed class ConversationSearchIndex
{
    private readonly ConversationSearchOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _sequence;
    private long _totalChars;

    /// <summary>Creates an empty index.</summary>
    /// <param name="options">The bounds, or <see langword="null"/> for the defaults.</param>
    public ConversationSearchIndex(ConversationSearchOptions? options = null)
    {
        _options = options ?? new ConversationSearchOptions();
    }

    /// <summary>Gets the number of sessions in the index.</summary>
    public int SessionCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Gets the number of transcript characters in the index.</summary>
    public long TotalChars
    {
        get
        {
            lock (_gate)
            {
                return _totalChars;
            }
        }
    }

    /// <summary>Adds or replaces a session. The session becomes the most recently updated, unless only its title changes (<paramref name="text"/> is null).</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="title">The session title.</param>
    /// <param name="text">The transcript text, or <see langword="null"/> to keep the text already indexed and update the title only.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    public void Upsert(string sessionId, string title, string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(title);

        lock (_gate)
        {
            _entries.TryGetValue(sessionId, out var existing);
            var kept = text is null
                ? existing?.Text ?? string.Empty
                : text.Length > _options.MaxCharsPerSession ? text[^_options.MaxCharsPerSession..] : text;
            _totalChars += kept.Length - (existing?.Text.Length ?? 0);
            _entries[sessionId] = new Entry(sessionId, title, kept, text is null && existing is not null ? existing.Sequence : ++_sequence);
            Trim();
        }
    }

    /// <summary>Removes a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns><see langword="true"/> when the session was in the index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sessionId"/> is null.</exception>
    public bool Remove(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        lock (_gate)
        {
            if (!_entries.Remove(sessionId, out var removed))
            {
                return false;
            }

            _totalChars -= removed.Text.Length;
            return true;
        }
    }

    /// <summary>Removes every session whose identifier is not in <paramref name="keep"/>.</summary>
    /// <param name="keep">The identifiers to keep.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keep"/> is null.</exception>
    public void RetainOnly(IReadOnlySet<string> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);

        lock (_gate)
        {
            foreach (var id in _entries.Keys.Where(id => !keep.Contains(id)).ToList())
            {
                _totalChars -= _entries[id].Text.Length;
                _entries.Remove(id);
            }
        }
    }

    /// <summary>Searches the titles and the transcripts.</summary>
    /// <param name="query">The query. An empty or blank query matches nothing.</param>
    /// <param name="limit">The most results.</param>
    /// <returns>The matching sessions, best first: a title match before a transcript match, then the most recently updated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public IReadOnlyList<ConversationSearchResult> Search(string query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);

        var bounded = query.Length > _options.MaxQueryChars ? query[.._options.MaxQueryChars] : query;
        var terms = bounded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(_options.MaxQueryTerms)
            .ToArray();
        if (terms.Length == 0 || limit <= 0)
        {
            return [];
        }

        var hits = new List<(ConversationSearchResult Result, long Sequence)>();
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                if (Match(entry, terms) is { } result)
                {
                    hits.Add((result, entry.Sequence));
                }
            }
        }

        return hits
            .OrderByDescending(static hit => hit.Result.Score)
            .ThenByDescending(static hit => hit.Sequence)
            .ThenBy(static hit => hit.Result.Title, StringComparer.Ordinal)
            .Take(limit)
            .Select(static hit => hit.Result)
            .ToList();
    }

    private ConversationSearchResult? Match(Entry entry, string[] terms)
    {
        var inTitle = true;
        var score = 0.0;
        var firstTextHit = -1;
        var firstTextLength = 0;
        foreach (var term in terms)
        {
            var inThisTitle = entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase);
            var textAt = entry.Text.Length == 0 ? -1 : entry.Text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (!inThisTitle && textAt < 0)
            {
                return null;
            }

            inTitle &= inThisTitle;
            if (textAt >= 0 && (firstTextHit < 0 || textAt < firstTextHit))
            {
                firstTextHit = textAt;
                firstTextLength = term.Length;
            }

            score += inThisTitle ? (entry.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 120 : 100) : 10;
        }

        if (inTitle)
        {
            score += 1000;
        }

        if (firstTextHit < 0)
        {
            return new ConversationSearchResult(entry.Id, entry.Title, string.Empty, -1, 0, inTitle, score);
        }

        var (snippet, start) = Snippet(entry.Text, firstTextHit, firstTextLength);
        return new ConversationSearchResult(entry.Id, entry.Title, snippet, start, firstTextLength, inTitle, score);
    }

    private (string Snippet, int Start) Snippet(string text, int at, int length)
    {
        var from = Math.Max(at - _options.SnippetRadius, 0);
        var to = Math.Min(at + length + _options.SnippetRadius, text.Length);
        var window = text[from..to];
        var builder = new System.Text.StringBuilder(window.Length + 2);
        var prefix = from > 0 ? "\u2026" : string.Empty;
        builder.Append(prefix);
        var matchStart = prefix.Length;
        var spaceRun = false;
        for (var index = 0; index < window.Length; index++)
        {
            var absolute = from + index;
            var character = window[index];
            if (char.IsWhiteSpace(character))
            {
                if (spaceRun)
                {
                    continue;
                }

                spaceRun = true;
                character = ' ';
            }
            else
            {
                spaceRun = false;
            }

            if (absolute < at)
            {
                matchStart++;
            }

            builder.Append(character);
        }

        if (to < text.Length)
        {
            builder.Append('\u2026');
        }

        return (builder.ToString(), matchStart);
    }

    private void Trim()
    {
        // Past the session bound the least recently updated session goes entirely.
        while (_entries.Count > _options.MaxSessions)
        {
            var oldest = _entries.Values.MinBy(static entry => entry.Sequence)!;
            _entries.Remove(oldest.Id);
            _totalChars -= oldest.Text.Length;
        }

        // Past the character bound the text of the least recently updated sessions goes, and their titles stay searchable.
        while (_totalChars > _options.MaxTotalChars)
        {
            var oldest = _entries.Values.Where(static entry => entry.Text.Length > 0).MinBy(static entry => entry.Sequence);
            if (oldest is null)
            {
                break;
            }

            _totalChars -= oldest.Text.Length;
            _entries[oldest.Id] = oldest with { Text = string.Empty };
        }
    }

    private sealed record Entry(string Id, string Title, string Text, long Sequence);
}
