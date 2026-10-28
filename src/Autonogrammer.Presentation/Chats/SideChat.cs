using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Chats;

/// <summary>
/// A side chat: a conversation opened from a session and kept apart from that session's transcript. Its messages
/// live only in <see cref="Messages"/>; nothing a side chat holds is folded into the parent session's transcript.
/// </summary>
public sealed class SideChat : ObservableModel
{
    private IReadOnlyList<SideChatMessage> _messages = [];

    /// <summary>Creates an empty side chat linked to its parent session.</summary>
    /// <param name="parentSessionId">The identifier of the session the side chat was opened from.</param>
    /// <param name="id">The side chat's identifier, unique within its parent session.</param>
    /// <param name="title">The label shown for the side chat.</param>
    /// <exception cref="ArgumentException">A parameter is null, empty or white space.</exception>
    public SideChat(string parentSessionId, string id, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ParentSessionId = parentSessionId;
        Id = id;
        Title = title;
    }

    /// <summary>Gets the identifier of the session this side chat was opened from.</summary>
    public string ParentSessionId { get; }

    /// <summary>Gets the side chat's identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the label shown for the side chat.</summary>
    public string Title { get; }

    /// <summary>Gets the side chat's messages, in the order they were posted. This list is separate from the parent transcript.</summary>
    public IReadOnlyList<SideChatMessage> Messages { get => _messages; private set => SetProperty(ref _messages, value); }

    /// <summary>Appends a message to this side chat only.</summary>
    /// <param name="author">Who wrote the message.</param>
    /// <param name="text">The message text.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or white space.</exception>
    public void Post(SideChatAuthor author, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // A new array per post makes the change observable: a list mutated in place would be the same reference.
        Messages = [.. _messages, new SideChatMessage { Author = author, Text = text }];
    }
}
