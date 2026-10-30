namespace Autonogrammer.Presentation.Chats;

/// <summary>One message in a side chat. The text is kept verbatim.</summary>
public sealed record SideChatMessage
{
    /// <summary>Gets who wrote the message.</summary>
    public required SideChatAuthor Author { get; init; }

    /// <summary>Gets the message text.</summary>
    public required string Text { get; init; }
}
