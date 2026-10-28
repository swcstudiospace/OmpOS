using Autonogrammer.Presentation.Components;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.State;

/// <summary>
/// The session list as an observable model (UI-SPEC discretion 13): its load state and its conversations. Each change is
/// announced through <see cref="ObservableModel.PropertyChanged"/>, so a bound list redraws while it is shown.
/// </summary>
public sealed class ConversationListModel : ObservableModel
{
    private ConversationListState _state = ConversationListState.Loading;
    private IReadOnlyList<ConversationSummary> _conversations = [];

    /// <summary>Gets the load state. The list starts in <see cref="ConversationListState.Loading"/>.</summary>
    public ConversationListState State { get => _state; private set => SetProperty(ref _state, value); }

    /// <summary>Gets the conversations, in the order they were loaded. Empty until the list is loaded.</summary>
    public IReadOnlyList<ConversationSummary> Conversations { get => _conversations; private set => SetProperty(ref _conversations, value); }

    /// <summary>Shows the loaded conversations, replacing any shown before. The list becomes <see cref="ConversationListState.Loaded"/>.</summary>
    /// <param name="conversations">The conversations to show, in display order. An empty list shows the empty prompt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conversations"/> is null.</exception>
    public void Load(IReadOnlyList<ConversationSummary> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        Conversations = [.. conversations];
        State = ConversationListState.Loaded;
    }

    /// <summary>Shows that the session list could not load. The list becomes <see cref="ConversationListState.Failed"/> with no conversations.</summary>
    public void Fail()
    {
        Conversations = [];
        State = ConversationListState.Failed;
    }
}
