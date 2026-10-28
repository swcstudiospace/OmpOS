using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Views.SideChats;

/// <summary>
/// The side chat pane of a session: each open side chat as a card with its messages, an ask box (Enter sends, Shift+Enter starts a new line), a Send and a Close button, an answering line and a
/// failure line, a New side chat control, and an optional Fork this chat button. It draws the bound <see cref="SideChatHub"/>. The view follows the hub by itself: the hub raises a change for its
/// <see cref="SideChatHub.Open"/> list when a side chat opens or closes, so the shell never has to refresh the pane. Cards that stay open keep their draft text.
/// </summary>
internal partial class SideChatPaneView : UserControl
{
    private readonly ObservableCollection<SideChatModel> _cards = [];
    private SideChatHub? _hub;
    private ForkModel? _fork;
    private string? _localText;

    /// <summary>Creates the pane with no hub.</summary>
    public SideChatPaneView()
    {
        InitializeComponent();
        OpenList.ItemsSource = _cards;

        // Enter is handled while it tunnels down, before the text box can use it for itself.
        NewQuestionBox.AddHandler(KeyDownEvent, OnNewQuestionKeyDown, RoutingStrategies.Tunnel);
        Render();
    }

    /// <summary>
    /// Gets or sets the fork model of the session. The Fork this chat button is shown while it is set and calls <see cref="ForkModel.DuplicateAsync"/>; the model's <see cref="ForkModel.Forked"/>
    /// event is what the head listens to for opening the new session.
    /// </summary>
    public ForkModel? Fork
    {
        get => _fork;
        set
        {
            if (ReferenceEquals(_fork, value))
            {
                return;
            }

            if (_fork is not null)
            {
                _fork.PropertyChanged -= OnForkChanged;
            }

            _fork = value;
            if (_fork is not null)
            {
                _fork.PropertyChanged += OnForkChanged;
            }

            RenderFork();
        }
    }

    /// <summary>Gets the box for the first question of a new side chat.</summary>
    internal TextBox NewQuestion => NewQuestionBox;

    /// <summary>Gets the New side chat button.</summary>
    internal Button NewSideChat => NewSideChatButton;

    /// <summary>Gets the Fork this chat button.</summary>
    internal Button ForkChat => ForkButton;

    /// <summary>Gets the status line of the hub, which is also where a failed command is told.</summary>
    internal TextBlock HubStatus => HubStatusText;

    /// <summary>Gets the status line of the fork.</summary>
    internal TextBlock ForkStatus => ForkStatusText;

    /// <summary>Gets the sentence shown while no side chat is open.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the list of side chat cards.</summary>
    internal ItemsControl Chats => OpenList;

    /// <summary>Gets the ask box of a side chat's card, or null when the card is not drawn.</summary>
    /// <param name="chat">The side chat.</param>
    /// <returns>The box.</returns>
    internal TextBox? AskBoxOf(SideChatModel chat) => PartOf<TextBox>(chat, "AskBox");

    /// <summary>Gets the Send button of a side chat's card, or null when the card is not drawn.</summary>
    /// <param name="chat">The side chat.</param>
    /// <returns>The button.</returns>
    internal Button? SendOf(SideChatModel chat) => PartOf<Button>(chat, "SendButton");

    /// <summary>Gets the Close button of a side chat's card, or null when the card is not drawn.</summary>
    /// <param name="chat">The side chat.</param>
    /// <returns>The button.</returns>
    internal Button? CloseOf(SideChatModel chat) => PartOf<Button>(chat, "CloseChatButton");

    /// <summary>Gets the line of a side chat's card that says the assistant is answering, or null when the card is not drawn.</summary>
    /// <param name="chat">The side chat.</param>
    /// <returns>The text block.</returns>
    internal TextBlock? AnsweringOf(SideChatModel chat) => PartOf<TextBlock>(chat, "AnsweringText");

    /// <summary>Gets the failure line of a side chat's card, or null when the card is not drawn.</summary>
    /// <param name="chat">The side chat.</param>
    /// <returns>The text block.</returns>
    internal TextBlock? ErrorOf(SideChatModel chat) => PartOf<TextBlock>(chat, "ErrorText");

    /// <summary>Binds the pane to the hub in the data context and draws its open side chats.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_hub is not null)
        {
            _hub.PropertyChanged -= OnHubChanged;
        }

        _localText = null;
        _hub = DataContext as SideChatHub;
        if (_hub is not null)
        {
            _hub.PropertyChanged += OnHubChanged;
        }

        SyncCards(focusChanges: false);
        Render();
    }

    private T? PartOf<T>(SideChatModel chat, string name)
        where T : Control
    {
        var index = _cards.IndexOf(chat);
        return index < 0
            ? null
            : OpenList.ContainerFromIndex(index)?.GetVisualDescendants().OfType<T>().FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
    }

    // The hub reports its expected failures on the models and never throws them. A fault that still escapes is shown on the status line instead of being lost on a task nobody observes.
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        _localText = null;
        try
        {
            await work(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            _localText = "Side chat command failed: " + exception.Message.TrimEnd('.') + ".";
            Render();
        }
    }

    private void OnHubChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUiThread(() =>
        {
            if (string.Equals(e.PropertyName, nameof(SideChatHub.Open), StringComparison.Ordinal))
            {
                SyncCards(focusChanges: true);
            }
            else if (string.Equals(e.PropertyName, nameof(SideChatHub.StatusText), StringComparison.Ordinal))
            {
                _localText = null;
            }

            Render();
        });

    private void OnForkChanged(object? sender, PropertyChangedEventArgs e) => OnUiThread(RenderFork);

    private static void OnUiThread(Action action)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    // Cards are added and removed one by one, so a card that stays open keeps its draft text and its scroll position.
    private void SyncCards(bool focusChanges)
    {
        IReadOnlyList<SideChatModel> open = _hub?.Open ?? [];
        var removed = false;
        foreach (var gone in _cards.Where(card => !open.Contains(card)).ToList())
        {
            removed |= _cards.Remove(gone);
        }

        SideChatModel? added = null;
        foreach (var chat in open)
        {
            if (!_cards.Contains(chat))
            {
                _cards.Add(chat);
                added = chat;
            }
        }

        EmptyText.IsVisible = _cards.Count == 0;
        if (!focusChanges)
        {
            return;
        }

        if (added is { } newChat)
        {
            Dispatcher.UIThread.Post(() => FocusAskBox(newChat, 3), DispatcherPriority.Input);
        }
        else if (removed && FocusIsInPaneOrLost())
        {
            NewSideChatButton.FocusWhenReady();
        }
    }

    // Focus counts as lost when nothing has it or the control that had it left the tree with its card.
    private bool FocusIsInPaneOrLost()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || ReferenceEquals(focused, this) || focused.GetVisualRoot() is null || focused.GetVisualAncestors().Contains(this);
    }

    // The card of a new side chat is realized by the next layout pass, so the focus request waits for it a few turns.
    private void FocusAskBox(SideChatModel chat, int attempts)
    {
        if (AskBoxOf(chat) is { } box && box.Focus())
        {
            return;
        }

        if (attempts > 0)
        {
            Dispatcher.UIThread.Post(() => FocusAskBox(chat, attempts - 1), DispatcherPriority.Background);
        }
    }

    private void Render()
    {
        var hasHub = _hub is not null;
        NewQuestionBox.IsEnabled = hasHub;
        NewSideChatButton.IsEnabled = hasHub;

        var text = _localText ?? _hub?.StatusText ?? string.Empty;
        HubStatusText.Text = text;
        HubStatusText.IsVisible = text.Length > 0;
        RenderFork();
    }

    private void RenderFork()
    {
        var fork = _fork;
        ForkButton.IsVisible = fork is not null;
        ForkButton.IsEnabled = fork is { IsBusy: false };
        var text = fork?.StatusText ?? string.Empty;
        var isError = fork?.HasError == true;
        ForkStatusText.Text = text;
        ForkStatusText.IsVisible = text.Length > 0;
        ForkStatusText.Classes.Set("tone-error", isError);
        AutomationProperties.SetLiveSetting(ForkStatusText, isError ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    private void OnNewSideChatClick(object? sender, RoutedEventArgs e) => StartSideChat();

    private void StartSideChat()
    {
        if (_hub is not { } hub)
        {
            return;
        }

        var question = NewQuestionBox.Text ?? string.Empty;
        NewQuestionBox.Text = string.Empty;
        _ = RunAsync(token => hub.OpenAsync(question, token));
    }

    private void OnForkClick(object? sender, RoutedEventArgs e)
    {
        if (_fork is { IsBusy: false } fork)
        {
            _ = RunAsync(token => fork.DuplicateAsync(token));
        }
    }

    private void OnCloseChatClick(object? sender, RoutedEventArgs e)
    {
        if (_hub is { } hub && (sender as Control)?.DataContext is SideChatModel chat)
        {
            _ = RunAsync(token => hub.CloseAsync(chat, token));
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not SideChatModel chat)
        {
            return;
        }

        var card = control.FindAncestorOfType<Border>();
        var box = card?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(candidate => string.Equals(candidate.Name, "AskBox", StringComparison.Ordinal));
        if (box is not null)
        {
            Ask(chat, box);
        }
    }

    // The question leaves the box at once, so the user can type the next one; when the question was refused or failed the text comes back unless the user already typed something else.
    private void Ask(SideChatModel chat, TextBox box)
    {
        var question = box.Text ?? string.Empty;
        var sent = !chat.IsAnswering && !chat.IsClosed && !string.IsNullOrWhiteSpace(question);
        if (sent)
        {
            box.Text = string.Empty;
        }

        _ = RunAsync(
            async token =>
            {
                var answered = await chat.AskAsync(question, token).ConfigureAwait(true);
                if (sent && !answered && string.IsNullOrEmpty(box.Text))
                {
                    box.Text = question;
                }
            });
    }

    private void OnAskBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.RemoveHandler(KeyDownEvent, OnAskKeyDown);
            box.AddHandler(KeyDownEvent, OnAskKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private void OnAskKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && sender is TextBox { DataContext: SideChatModel chat } box)
        {
            e.Handled = true;
            Ask(chat, box);
        }
    }

    private void OnNewQuestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            StartSideChat();
        }
    }
}
