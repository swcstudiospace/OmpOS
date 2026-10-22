using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Presentation.Components;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Views;

/// <summary>
/// The session list. It draws the presentation <see cref="ConversationListState"/>: the loading, empty and failed
/// copy, or one row per <see cref="ConversationSummary"/>. Each row is named by its full title.
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

    private IReadOnlyList<ConversationSummary> _rows = Array.Empty<ConversationSummary>();

    /// <summary>Creates the list in the loading state with no conversations.</summary>
    public ConversationListView()
    {
        InitializeComponent();
        ConversationRows.ContainerPrepared += OnContainerPrepared;
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
    }

    /// <summary>Redraws the rows and the state copy from <see cref="State"/> and <see cref="Conversations"/>.</summary>
    public void Refresh()
    {
        _rows = State == ConversationListState.Loaded ? Conversations : Array.Empty<ConversationSummary>();
        ConversationRows.ItemsSource = _rows;
        ApplyStateCopy();
    }

    private void ApplyStateCopy()
    {
        var (heading, body, isError) = State switch
        {
            ConversationListState.Loading => ("Loading conversations…", string.Empty, false),
            ConversationListState.Failed => ("Could not load conversations.", "Close and reopen OmpOS to try again.", true),
            _ when Conversations.Count == 0 => ("No conversations yet.", "Conversations you start will appear here.", false),
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
}
