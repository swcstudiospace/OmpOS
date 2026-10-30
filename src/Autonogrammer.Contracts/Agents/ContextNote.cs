namespace Autonogrammer.Contracts.Agents;

/// <summary>
/// One piece of shared context, a remembered fact or a lesson from earlier work. The text is untrusted, plain text.
/// </summary>
public sealed record ContextNote
{
    /// <summary>Gets the identifier of the note in its origin.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the note text. Consumers show it as text and never as markup.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the name of the place the note came from, for example the memory scope.</summary>
    public required string Origin { get; init; }

    /// <summary>Gets the time the note was recorded, or null when the origin does not say.</summary>
    public DateTimeOffset? RecordedAt { get; init; }
}
