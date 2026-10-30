namespace Autonogrammer.Presentation.Components;

/// <summary>
/// One row in the conversation list.
/// </summary>
public sealed record ConversationSummary
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display title.</summary>
    public required string Title { get; init; }
}
