namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>One shared-memory entry or lesson as the Context tab shows it. Untrusted text is plain: control and bidirectional characters are removed and the text is capped.</summary>
public sealed record ContextNoteRow
{
    /// <summary>Gets the identifier of the note in its origin.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the note text capped at <see cref="TaskContextModel.MaxNoteChars"/> characters.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the note text cut at <see cref="TaskContextModel.NoteDisplayChars"/> characters with an ellipsis, as the tab draws it.</summary>
    public required string DisplayText { get; init; }

    /// <summary>Gets the name of the place the note came from.</summary>
    public required string Origin { get; init; }

    /// <summary>Gets the meta line: <c>{Origin} · {relative time}</c>, without the time when the origin does not give one.</summary>
    public required string MetaText { get; init; }

    /// <summary>Gets the time the note was recorded, or null.</summary>
    public DateTimeOffset? RecordedAt { get; init; }
}
