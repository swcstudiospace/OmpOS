using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>One list of the Context tab (shared memory or lessons): its header with count, the kept notes, the notes the tab shows, and the overflow line.</summary>
public sealed record ContextNoteList
{
    /// <summary>Gets the header with the count in parentheses, for example <c>Shared memory (3)</c>.</summary>
    public required string Header { get; init; }

    /// <summary>Gets the number of notes the source returned, including those beyond the kept cap.</summary>
    public required int Total { get; init; }

    /// <summary>Gets the kept notes, at most <see cref="TaskContextModel.MaxNotes"/>.</summary>
    public required IReadOnlyList<ContextNoteRow> Notes { get; init; }

    /// <summary>Gets the notes the tab shows, at most <see cref="TaskContextModel.NotesShown"/>.</summary>
    public required IReadOnlyList<ContextNoteRow> Visible { get; init; }

    /// <summary>Gets <c>+{n} more not shown</c> when notes are not shown, or null.</summary>
    public string? MoreText { get; init; }

    /// <summary>Gets the line shown while the list is empty.</summary>
    public required string EmptyText { get; init; }

    /// <summary>Gets whether the list has no note.</summary>
    public bool IsEmpty => Total == 0;

    /// <summary>Builds the list for a set of notes.</summary>
    /// <param name="title">The header title.</param>
    /// <param name="emptyText">The line for an empty list.</param>
    /// <param name="notes">The notes from the source. Null means none.</param>
    /// <param name="now">The current time, for the relative times.</param>
    /// <returns>The list.</returns>
    internal static ContextNoteList Build(string title, string emptyText, IReadOnlyList<ContextNote>? notes, DateTimeOffset now)
    {
        var source = notes ?? [];
        var kept = source.Take(TaskContextModel.MaxNotes).Select(note => ToRow(note, now)).ToList();
        var visible = kept.Take(TaskContextModel.NotesShown).ToList();
        var hidden = source.Count - visible.Count;
        return new ContextNoteList
        {
            Header = $"{title} ({source.Count})",
            Total = source.Count,
            Notes = kept,
            Visible = visible,
            MoreText = hidden > 0 ? $"+{hidden} more not shown" : null,
            EmptyText = emptyText,
        };
    }

    private static ContextNoteRow ToRow(ContextNote note, DateTimeOffset now)
    {
        var origin = PanelText.Plain(note.Origin, 80);
        var relative = note.RecordedAt is { } recorded ? RelativeTimeText.Format(recorded, now) : null;
        var meta = relative is null ? origin : origin.Length == 0 ? relative : $"{origin} \u00B7 {relative}";
        return new ContextNoteRow
        {
            Id = PanelText.Plain(note.Id, 80),
            Text = PanelText.Plain(note.Text, TaskContextModel.MaxNoteChars),
            DisplayText = PanelText.Plain(note.Text, TaskContextModel.NoteDisplayChars),
            Origin = origin,
            MetaText = meta,
            RecordedAt = note.RecordedAt,
        };
    }
}
