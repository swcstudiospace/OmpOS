using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.SideChats;

/// <summary>
/// The side chats opened from one session, through <see cref="SideChatPanel"/> (so the existing side chat list keeps working) with an engine answering each. <see cref="OpenAsync"/> is what
/// <c>/side</c> and <c>/btw</c> run: it takes a read-only summary of the parent at that moment, opens a side chat and, when the user typed a question after the command, asks it. Each side chat closes on its own.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class SideChatHub : ObservableModel
{
    private readonly SideChatPanel _panel;
    private readonly ISideChatEngine _engine;
    private readonly Func<IReadOnlyList<UiBlock>> _parentBlocks;
    private readonly List<SideChatModel> _open = [];
    private string _statusText = string.Empty;

    /// <summary>Creates the hub of one session.</summary>
    /// <param name="panel">The session's side chat list. The hub opens chats through it.</param>
    /// <param name="engine">Answers the questions of every side chat.</param>
    /// <param name="parentBlocks">Returns the parent's transcript blocks now, read-only.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SideChatHub(SideChatPanel panel, ISideChatEngine engine, Func<IReadOnlyList<UiBlock>> parentBlocks)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(parentBlocks);
        _panel = panel;
        _engine = engine;
        _parentBlocks = parentBlocks;
    }

    /// <summary>Gets the side chats that are open, in the order they were opened.</summary>
    public IReadOnlyList<SideChatModel> Open => _open;

    /// <summary>Gets the last sentence for the status line.</summary>
    public string StatusText => _statusText;

    /// <summary>Opens a side chat from the session, like <c>/side</c> or <c>/btw</c>.</summary>
    /// <param name="question">The text typed after the command, or an empty string to open an empty chat.</param>
    /// <param name="cancellationToken">Cancels the first question.</param>
    /// <returns>The side chat. When <paramref name="question"/> is not blank it has been asked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="question"/> is null.</exception>
    public async Task<SideChatModel> OpenAsync(string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        var chat = _panel.Open();
        var model = new SideChatModel(chat, ParentSummaryBuilder.Build(_parentBlocks()), _engine);
        _open.Add(model);
        NotifyPropertyChanged(nameof(Open));
        if (!string.IsNullOrWhiteSpace(question))
        {
            await model.AskAsync(question, cancellationToken).ConfigureAwait(true);
        }

        return model;
    }

    /// <summary>Refuses to open a side chat from a side chat.</summary>
    /// <param name="from">The side chat the user tried to open another from.</param>
    /// <returns>Always <see langword="null"/>; <see cref="StatusText"/> says that side chats do not nest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="from"/> is null.</exception>
    public SideChatModel? OpenFrom(SideChatModel from)
    {
        ArgumentNullException.ThrowIfNull(from);
        SetProperty(ref _statusText, SideChatCopy.NoNesting, nameof(StatusText));
        return null;
    }

    /// <summary>Closes one side chat and removes it from <see cref="Open"/>. The other side chats and the parent session are not touched.</summary>
    /// <param name="chat">The side chat.</param>
    /// <param name="cancellationToken">Cancels the close of the child conversation.</param>
    /// <returns>A task that completes when the side chat is closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chat"/> is null.</exception>
    public async Task CloseAsync(SideChatModel chat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chat);
        await chat.CloseAsync(cancellationToken).ConfigureAwait(true);
        if (_open.Remove(chat))
        {
            NotifyPropertyChanged(nameof(Open));
        }
    }
}
