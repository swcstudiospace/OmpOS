namespace OmpOS.Presentation.Mentions;

/// <summary>What the picker's message line says.</summary>
public enum MentionMessageKind
{
    /// <summary>Nothing: rows are listed.</summary>
    None = 0,

    /// <summary>A search is running and no row is listed yet.</summary>
    Searching = 1,

    /// <summary>Nothing matches the query.</summary>
    Empty = 2,

    /// <summary>Every source failed; the message names the reason.</summary>
    Error = 3,

    /// <summary>A source cannot answer (for example no pull request host) or the selected row is disabled; the message names the reason.</summary>
    Unavailable = 4,

    /// <summary>The user chose a file that may hold secrets and must confirm.</summary>
    Confirm = 5,
}
