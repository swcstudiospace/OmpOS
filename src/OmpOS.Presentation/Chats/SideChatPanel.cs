using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Chats;

/// <summary>
/// The side chats opened from one session, with the selected one. Opening a side chat creates it here and selects it;
/// the parent session is never touched, so its transcript keeps the same blocks.
/// </summary>
public sealed class SideChatPanel : ObservableModel
{
    private IReadOnlyList<SideChat> _chats = [];
    private SideChat? _selected;

    /// <summary>Creates the panel for one session.</summary>
    /// <param name="parentSessionId">The identifier of the session the side chats are opened from.</param>
    /// <exception cref="ArgumentException"><paramref name="parentSessionId"/> is null, empty or white space.</exception>
    public SideChatPanel(string parentSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentSessionId);
        ParentSessionId = parentSessionId;
    }

    /// <summary>Gets the identifier of the session the side chats are opened from.</summary>
    public string ParentSessionId { get; }

    /// <summary>Gets the side chats opened from this session, in the order they were opened.</summary>
    public IReadOnlyList<SideChat> Chats { get => _chats; private set => SetProperty(ref _chats, value); }

    /// <summary>Gets the selected side chat, or null when none has been opened.</summary>
    public SideChat? Selected { get => _selected; private set => SetProperty(ref _selected, value); }

    /// <summary>Opens a new side chat from the session and selects it.</summary>
    /// <returns>The new side chat.</returns>
    public SideChat Open()
    {
        var ordinal = _chats.Count + 1;
        var chat = new SideChat(ParentSessionId, $"{ParentSessionId}/side-{ordinal}", $"Side chat {ordinal}");

        Chats = [.. _chats, chat];
        Selected = chat;
        return chat;
    }

    /// <summary>Selects one of the side chats opened from this session.</summary>
    /// <param name="chat">The side chat to select.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chat"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="chat"/> was not opened from this session.</exception>
    public void Select(SideChat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        if (!_chats.Contains(chat))
        {
            throw new ArgumentException("The side chat was not opened from this session.", nameof(chat));
        }

        Selected = chat;
    }
}
