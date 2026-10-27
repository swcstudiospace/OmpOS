using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Views.Kanban;

/// <summary>
/// The Kanban tab: the <see cref="KanbanBoardModel"/> that is its data context drawn as columns of task cards, or as the loading, empty or
/// error state. Host it with <c>new KanbanBoardView { DataContext = board }</c>.
/// </summary>
/// <remarks>
/// <para>
/// The ten column controls and the card controls are built once and kept: an update moves a card between columns and never rebuilds a card
/// that stays, so an open steer flyout and the text typed into it survive other sessions' updates.
/// </para>
/// <para>
/// Keyboard: the current card (and its Steer button) is the one tab stop of the board. The arrow keys move between cards and columns, Enter opens
/// the session of the focused card, S opens its steer flyout and Esc closes the flyout. Only Backlog and Archived accept a dropped card.
/// </para>
/// </remarks>
internal partial class KanbanBoardView : UserControl
{
    private const string SkeletonClass = "skeleton";
    private const string PulseClass = "skeleton-pulse";
    private const string DropTargetClass = "drop-target";

    private readonly ModelWatcher<KanbanBoardModel> _watch;
    private readonly Dictionary<string, KanbanCardView> _cardViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ColumnParts> _columns = new(StringComparer.Ordinal);
    private List<List<KanbanCardView>> _layout = [];
    private CancellationTokenSource? _detailsLifetime;
    private string _detailsKey = string.Empty;
    private string? _currentId;
    private bool _attached;

    /// <summary>Creates a board with no model.</summary>
    public KanbanBoardView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<KanbanBoardModel>(Refresh);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        Refresh();
    }

    /// <summary>Gets the board model, or null.</summary>
    internal KanbanBoardModel? Board => DataContext as KanbanBoardModel;

    /// <summary>Gets the controls that hold the loading skeleton.</summary>
    internal IEnumerable<Border> SkeletonBars => LoadingBlock.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Contains(SkeletonClass));

    /// <summary>Gets the number of skeleton bars that pulse at the moment. It is zero under reduced motion.</summary>
    internal int PulsingSkeletons => SkeletonBars.Count(static border => border.Classes.Contains(PulseClass));

    /// <summary>Gets the card controls in board order: column by column, top to bottom.</summary>
    internal IReadOnlyList<KanbanCardView> CardViews => [.. _layout.SelectMany(static column => column)];

    /// <summary>Gets the control of one card.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The card control, or null when the session is not on the board.</returns>
    internal KanbanCardView? CardViewOf(string sessionId) => _cardViews.GetValueOrDefault(sessionId);

    /// <summary>Gets the border of a column.</summary>
    /// <param name="columnKey">The column key.</param>
    /// <returns>The border, or null when the column has not been drawn.</returns>
    internal Border? ColumnOf(string columnKey) => _columns.GetValueOrDefault(columnKey)?.Frame;

    /// <summary>Gets the header text of a column.</summary>
    /// <param name="columnKey">The column key.</param>
    /// <returns>The header block, or null when the column has not been drawn.</returns>
    internal TextBlock? ColumnHeaderOf(string columnKey) => _columns.GetValueOrDefault(columnKey)?.Header;

    /// <summary>Gets the controls of the states and the board, for the tests and the font walk.</summary>
    internal IEnumerable<TextBlock> StateTexts => [LoadingHeading, LoadingBody, EmptyHeading, EmptyBody, ErrorHeading, ErrorBody, BannerText, LiveRegion];

    /// <summary>Gets the button of the empty state.</summary>
    internal Button StartSession => StartSessionButton;

    /// <summary>Gets the retry button of the error state.</summary>
    internal Button Retry => RetryButton;

    /// <summary>Gets the retry button of the banner above the columns.</summary>
    internal Button BannerRetry => BannerRetryButton;

    /// <summary>Gets the live region.</summary>
    internal TextBlock Live => LiveRegion;

    /// <summary>Gets the banner above the columns.</summary>
    internal Border Banner => ErrorBanner;

    /// <summary>Gets the card that is the board's current card: the one tab stop of the board.</summary>
    internal KanbanCardView? CurrentCard => _currentId is { } id ? _cardViews.GetValueOrDefault(id) : null;

    /// <summary>Moves keyboard focus to the current card.</summary>
    /// <returns>True when a card took the focus; false when the board has no card.</returns>
    public bool FocusCards()
    {
        if (CurrentCard is not { } card)
        {
            return false;
        }

        card.FocusWhenReady();
        return true;
    }

    /// <summary>Applies a card drop: moves the card to the column when the column accepts it.</summary>
    /// <param name="columnKey">The key of the column the card was dropped on.</param>
    /// <param name="sessionId">The dragged session, or null when the drag carried none.</param>
    /// <returns>What the board did. A drag without a session is <see cref="KanbanMoveOutcome.UnknownCard"/>.</returns>
    internal KanbanMoveOutcome HandleDrop(string columnKey, string? sessionId) =>
        sessionId is null || Board is not { } board ? KanbanMoveOutcome.UnknownCard : board.Move(sessionId, columnKey);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(Board);
        ApplyMotion();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
        StopDetails();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _watch.Set(Board);
        }

        Refresh();
    }

    private static KanbanCardView? CardOf(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is KanbanCardView card)
            {
                return card;
            }
        }

        return null;
    }

    private static void SetClass(StyledElement element, string name, bool on)
    {
        if (on)
        {
            if (!element.Classes.Contains(name))
            {
                element.Classes.Add(name);
            }
        }
        else
        {
            element.Classes.Remove(name);
        }
    }

    private void Refresh()
    {
        var board = Board;
        var state = board?.State;
        LoadingBlock.IsVisible = state == BoardState.Loading;
        EmptyBlock.IsVisible = state == BoardState.Empty;
        ErrorBlock.IsVisible = state == BoardState.Error;
        ColumnsScroller.IsVisible = state == BoardState.Ready;

        var banner = state == BoardState.Ready && board?.ErrorText is not null;
        ErrorBanner.IsVisible = banner;
        BannerText.Text = banner ? KanbanCopy.BannerLead + " " + board!.ErrorText : null;

        ApplyMotion();
        if (board is not null && state == BoardState.Ready)
        {
            SyncColumns(board);
            LoadDetailsWhenCardsChange(board);
        }
        else if (board is null)
        {
            SyncColumns(null);
        }
    }

    private void ApplyMotion()
    {
        var reduced = MotionProbe.IsReduced(this);
        foreach (var bar in SkeletonBars)
        {
            SetClass(bar, PulseClass, !reduced);
        }
    }

    private void SyncColumns(KanbanBoardModel? board)
    {
        var columns = board?.Columns ?? [];
        var models = board is null
            ? new Dictionary<string, KanbanCardViewModel>(StringComparer.Ordinal)
            : board.CardModels.ToDictionary(static model => model.Id, StringComparer.Ordinal);

        var desired = new Dictionary<string, List<KanbanCardView>>(StringComparer.Ordinal);
        var layout = new List<List<KanbanCardView>>(columns.Count);
        foreach (var column in columns)
        {
            var parts = GetOrCreateColumn(column);
            var views = new List<KanbanCardView>(column.Cards.Count);
            foreach (var card in column.Cards)
            {
                if (models.TryGetValue(card.Id, out var model))
                {
                    views.Add(GetOrCreateCard(model));
                }
            }

            desired[column.Key] = views;
            layout.Add(views);
            parts.Header.Text = $"{column.Title}  {column.Count}";
            AutomationProperties.SetName(parts.Frame, column.AccessibleName);
            parts.Empty.Text = column.AcceptsDrop ? KanbanCopy.DropHint : KanbanCopy.EmptyColumn;
            parts.Empty.IsVisible = views.Count == 0;
        }

        foreach (var gone in _cardViews.Keys.Where(id => !models.ContainsKey(id)).ToList())
        {
            _cardViews.Remove(gone);
        }

        ReconcileColumnChildren(desired);

        _layout = layout;
        EnsureCurrent();
    }

    private void ReconcileColumnChildren(Dictionary<string, List<KanbanCardView>> desired)
    {
        foreach (var (key, parts) in _columns)
        {
            var wanted = desired.GetValueOrDefault(key) ?? [];
            var children = parts.Cards.Children;
            for (var child = children.Count - 1; child >= 0; child--)
            {
                if (!wanted.Contains((KanbanCardView)children[child]))
                {
                    children.RemoveAt(child);
                }
            }
        }

        foreach (var (key, wanted) in desired)
        {
            var children = _columns[key].Cards.Children;
            for (var position = 0; position < wanted.Count; position++)
            {
                var view = wanted[position];
                if (position < children.Count && ReferenceEquals(children[position], view))
                {
                    continue;
                }

                var existing = children.IndexOf(view);
                if (existing >= 0)
                {
                    children.Move(existing, position);
                }
                else
                {
                    children.Insert(position, view);
                }
            }
        }
    }

    private ColumnParts GetOrCreateColumn(KanbanColumn column)
    {
        if (_columns.TryGetValue(column.Key, out var existing))
        {
            return existing;
        }

        var header = new TextBlock();
        header.Classes.Add("kanban-column-header");
        AutomationProperties.SetAccessibilityView(header, AccessibilityView.Raw);
        var empty = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        empty.Classes.Add("kanban-empty");
        var cards = new StackPanel();
        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(empty);
        content.Children.Add(cards);
        var frame = new Border { Child = content };
        frame.Classes.Add("kanban-column");

        DragDrop.SetAllowDrop(frame, column.AcceptsDrop);
        var key = column.Key;
        DragDrop.AddDragEnterHandler(frame, (_, args) => OnDragOver(frame, key, args));
        DragDrop.AddDragOverHandler(frame, (_, args) => OnDragOver(frame, key, args));
        DragDrop.AddDragLeaveHandler(frame, (_, _) => SetClass(frame, DropTargetClass, false));
        DragDrop.AddDropHandler(frame, (_, args) => OnDrop(frame, key, args));

        var parts = new ColumnParts(frame, header, empty, cards);
        _columns[column.Key] = parts;
        ColumnsPanel.Children.Add(frame);
        return parts;
    }

    private KanbanCardView GetOrCreateCard(KanbanCardViewModel model)
    {
        if (!_cardViews.TryGetValue(model.Id, out var view))
        {
            view = new KanbanCardView();
            _cardViews[model.Id] = view;
        }

        if (!ReferenceEquals(view.DataContext, model))
        {
            view.DataContext = model;
        }

        return view;
    }

    private void EnsureCurrent()
    {
        if (_currentId is null || !_cardViews.ContainsKey(_currentId))
        {
            _currentId = _layout.SelectMany(static column => column).FirstOrDefault()?.Model?.Id;
        }

        foreach (var (id, view) in _cardViews)
        {
            view.SetCurrent(string.Equals(id, _currentId, StringComparison.Ordinal));
        }
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (CardOf(e.Source) is { Model: { } model } && !string.Equals(model.Id, _currentId, StringComparison.Ordinal))
        {
            _currentId = model.Id;
            EnsureCurrent();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Source is TextBox || CardOf(e.Source) is not { } card)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                e.Handled = MoveFocus(card, 0, -1);
                break;
            case Key.Down:
                e.Handled = MoveFocus(card, 0, 1);
                break;
            case Key.Left:
                e.Handled = MoveFocus(card, -1, 0);
                break;
            case Key.Right:
                e.Handled = MoveFocus(card, 1, 0);
                break;
            case Key.Enter when ReferenceEquals(e.Source, card):
                e.Handled = true;
                card.Model?.OpenCommand.Execute(null);
                break;
            case Key.S:
                e.Handled = card.OpenSteer();
                break;
            default:
                break;
        }
    }

    private bool MoveFocus(KanbanCardView from, int columnStep, int rowStep)
    {
        var columnIndex = _layout.FindIndex(column => column.Contains(from));
        if (columnIndex < 0)
        {
            return false;
        }

        var row = _layout[columnIndex].IndexOf(from);
        KanbanCardView? target = null;
        if (rowStep != 0)
        {
            var next = row + rowStep;
            target = next >= 0 && next < _layout[columnIndex].Count ? _layout[columnIndex][next] : null;
        }
        else
        {
            for (var next = columnIndex + columnStep; next >= 0 && next < _layout.Count; next += columnStep)
            {
                if (_layout[next].Count > 0)
                {
                    target = _layout[next][Math.Min(row, _layout[next].Count - 1)];
                    break;
                }
            }
        }

        if (target is null)
        {
            return true;
        }

        target.Focus(NavigationMethod.Directional);
        target.BringIntoView();
        return true;
    }

    private void OnDragOver(Border frame, string key, DragEventArgs e)
    {
        var accepts = Board?.CanDrop(key) == true;
        e.DragEffects = accepts ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        SetClass(frame, DropTargetClass, accepts);
    }

    private void OnDrop(Border frame, string key, DragEventArgs e)
    {
        SetClass(frame, DropTargetClass, false);
        e.Handled = true;
        _ = HandleDrop(key, e.DataTransfer.TryGetText());
    }

    private void LoadDetailsWhenCardsChange(KanbanBoardModel board)
    {
        var key = string.Join('|', board.Cards.Select(static card => card.Id));
        if (!_attached || string.Equals(key, _detailsKey, StringComparison.Ordinal))
        {
            return;
        }

        _detailsKey = key;
        StopDetails();
        var lifetime = new CancellationTokenSource();
        _detailsLifetime = lifetime;
        _ = LoadDetailsAsync(board, lifetime.Token);
    }

    private void StopDetails()
    {
        _detailsLifetime?.Cancel();
        _detailsLifetime?.Dispose();
        _detailsLifetime = null;
        _detailsKey = string.Empty;
    }

    private static async Task LoadDetailsAsync(KanbanBoardModel board, CancellationToken cancellationToken)
    {
        try
        {
            await board.RefreshDetailsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The board was detached or its cards changed again; the next load replaces this one.
        }
    }

    /// <summary>The controls of one column.</summary>
    /// <param name="Frame">The column border.</param>
    /// <param name="Header">The title and count.</param>
    /// <param name="Empty">The text of a column without a card.</param>
    /// <param name="Cards">The panel that holds the card controls.</param>
    private sealed record ColumnParts(Border Frame, TextBlock Header, TextBlock Empty, StackPanel Cards);
}
