using System.ComponentModel;
using Autonogrammer.Presentation.Components;
using Autonogrammer.Presentation.State;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The session list. It draws the presentation <see cref="ConversationListState"/>: the loading, empty and failed
/// copy, or one row per <see cref="ConversationSummary"/>. Each row is named by its full title.
/// When its data context is a <see cref="ConversationListModel"/>, it draws that model and redraws it whenever the model
/// changes while the list is shown.
/// </summary>
internal partial class ConversationListView : UserControl
{
    /// <summary>The load state the list draws.</summary>
    public static readonly StyledProperty<ConversationListState> StateProperty =
        AvaloniaProperty.Register<ConversationListView, ConversationListState>(nameof(State), ConversationListState.Loading);

    /// <summary>The conversations to list. Rows are drawn only while <see cref="State"/> is <see cref="ConversationListState.Loaded"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<ConversationSummary>> ConversationsProperty =
        AvaloniaProperty.Register<ConversationListView, IReadOnlyList<ConversationSummary>>(
            nameof(Conversations),
            Array.Empty<ConversationSummary>());

    /// <summary>The selected conversation identifier. The row with that identifier is the list's current row.</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<ConversationListView, string?>(nameof(SelectedId));

    /// <summary>Gets or sets the identifier of the current conversation. The row with that identifier is selected, and none is when no row has it.</summary>
    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>Raised on the UI thread when the user chooses a conversation other than the current one. The argument names it.</summary>
    public event EventHandler<ConversationSelectedEventArgs>? ConversationSelected;

    private IReadOnlyList<ConversationSummary> _rows = Array.Empty<ConversationSummary>();
    private ConversationListModel? _model;

    /// <summary>Creates the list in the loading state with no conversations.</summary>
    public ConversationListView()
    {
        InitializeComponent();
        ConversationRows.ContainerPrepared += OnContainerPrepared;
        ConversationRows.SelectionChanged += OnRowSelectionChanged;
        ConversationRows.ContainerIndexChanged += OnContainerIndexChanged;
        Refresh();
    }

    /// <summary>Gets or sets the load state the list draws.</summary>
    public ConversationListState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Gets or sets the conversations to list.</summary>
    public IReadOnlyList<ConversationSummary> Conversations
    {
        get => GetValue(ConversationsProperty);
        set => SetValue(ConversationsProperty, value);
    }

    /// <summary>Redraws the list and its state copy. Runs when either property changes.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty || change.Property == ConversationsProperty)
        {
            Refresh();
        }
        else if (change.Property == SelectedIdProperty)
        {
            RenderSelection();
        }
    }

    /// <summary>Subscribes to the data-context model when the list is shown, and draws its current state.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BindModel(DataContext as ConversationListModel);
        Refresh();
    }

    /// <summary>Unsubscribes from the model when the list leaves the visual tree; a list that is not shown keeps what it drew.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        BindModel(null);
    }

    /// <summary>Follows a new data context while the list is shown.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (VisualRoot is not null)
        {
            BindModel(DataContext as ConversationListModel);
            Refresh();
        }
    }

    /// <summary>Redraws the rows and the state copy from the bound model when there is one, otherwise from <see cref="State"/> and <see cref="Conversations"/>.</summary>
    public void Refresh()
    {
        var state = _model?.State ?? State;
        var conversations = _model?.Conversations ?? Conversations;

        _rows = state == ConversationListState.Loaded ? conversations : Array.Empty<ConversationSummary>();
        ConversationRows.ItemsSource = _rows;
        RenderSelection();
        ApplyStateCopy(state, conversations.Count);
    }

    /// <summary>Selects the row whose conversation is the current one, or none when no row has that identifier.</summary>
    private void RenderSelection()
    {
        ConversationRows.SelectedItem = SelectedId is null
            ? null
            : _rows.FirstOrDefault(row => string.Equals(row.Id, SelectedId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reports a row the user chose. Drawing the current row also selects it, and that is not a choice: only a row that is in the list and
    /// is not already the current one is reported, and it becomes the current one.
    /// </summary>
    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ConversationRows.SelectedItem is not ConversationSummary row
            || string.Equals(row.Id, SelectedId, StringComparison.Ordinal)
            || !_rows.Contains(row))
        {
            return;
        }

        SelectedId = row.Id;
        ConversationSelected?.Invoke(this, new ConversationSelectedEventArgs(row.Id));
    }

    private void ApplyStateCopy(ConversationListState state, int count)
    {
        var (heading, body, isError) = state switch
        {
            ConversationListState.Loading => ("Loading conversations…", string.Empty, false),
            ConversationListState.Failed => ("Could not load conversations.", "Close and reopen Autonogrammer to try again.", true),
            _ when count == 0 => ("No conversations yet.", "Conversations you start will appear here.", false),
            _ => (string.Empty, string.Empty, false),
        };

        StatePanel.IsVisible = heading.Length > 0;
        StateHeading.Text = heading;
        StateBody.Text = body;
        StateBody.IsVisible = body.Length > 0;
        StateHeading.Classes.Set("error", isError);
        StateBody.Classes.Set("error", isError);
        StateBody.Classes.Set("muted", !isError);
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e) => NameRow(e.Container, e.Index);

    private void OnContainerIndexChanged(object? sender, ContainerIndexChangedEventArgs e) => NameRow(e.Container, e.NewIndex);

    private void NameRow(Control container, int index)
    {
        if (index >= 0 && index < _rows.Count)
        {
            AutomationProperties.SetName(container, _rows[index].Title);
        }
    }

    private void BindModel(ConversationListModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = model;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUiThread(() =>
        {
            // A change made while the list is not shown is not drawn; the list catches up when it is attached again.
            if (_model is not null)
            {
                Refresh();
            }
        });

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
}
