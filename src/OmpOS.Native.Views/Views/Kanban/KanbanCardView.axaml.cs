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
/// One Kanban task card: title, Chat or Terminal tag, model and provider, the status chip, last activity, the stale, disconnected and closed
/// words, the Substrate fields and a Steer button whose flyout sends a message to the session. It draws the <see cref="KanbanCardViewModel"/>
/// that is its data context. The card is a keyboard stop when it is the board's current card (roving tab stop); the board moves the focus
/// between cards with the arrow keys.
/// </summary>
internal partial class KanbanCardView : UserControl
{
    private const double DragThreshold = 6;

    private readonly ModelWatcher<KanbanCardViewModel> _watch;
    private PointerPressedEventArgs? _press;
    private bool _attached;

    /// <summary>Creates a card with no model.</summary>
    public KanbanCardView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<KanbanCardViewModel>(Refresh);
        SteerFlyout.Opened += OnSteerOpened;
        SteerFlyout.Closed += OnSteerClosed;
        SteerPanel.AddHandler(KeyDownEvent, OnSteerKeyDown, RoutingStrategies.Tunnel);
        Refresh();
    }

    private Flyout SteerFlyout => (Flyout)SteerButton.Flyout!;

    /// <summary>Gets the view model the card draws, or null.</summary>
    internal KanbanCardViewModel? Model => DataContext as KanbanCardViewModel;

    /// <summary>Gets the steer button.</summary>
    internal Button SteerTrigger => SteerButton;

    /// <summary>Gets the steering message box of the flyout.</summary>
    internal TextBox SteerInput => SteerBox;

    /// <summary>Gets the button that delivers the message now.</summary>
    internal Button SendNowTrigger => SendNowButton;

    /// <summary>Gets the button that queues the message for the next turn.</summary>
    internal Button QueueTrigger => QueueButton;

    /// <summary>Gets the line that shows the result of the last steering attempt.</summary>
    internal TextBlock SteerResultLine => SteerResult;

    /// <summary>Gets the status chip.</summary>
    internal StatusChip Status => StatusChip;

    /// <summary>Gets whether the steer flyout is open.</summary>
    internal bool IsSteerOpen => SteerFlyout.IsOpen;

    /// <summary>Gets the controls that hold text, for a font walk.</summary>
    internal IEnumerable<TextBlock> TextParts => [TitleText, KindText, ModelText, ActivityText, StaleText, DisconnectedText, ClosedText, TaskField, GraphField, LessonsField, LeaseField];

    /// <summary>Makes the card the board's current card: only the current card and its Steer button are tab stops.</summary>
    /// <param name="current">True for the current card.</param>
    internal void SetCurrent(bool current)
    {
        IsTabStop = current;
        SteerButton.IsTabStop = current;
    }

    /// <summary>Opens the steer flyout, which takes focus into the message box.</summary>
    /// <returns>True when the flyout was opened; false when the card has no steering service.</returns>
    internal bool OpenSteer()
    {
        if (!SteerButton.IsEffectivelyEnabled)
        {
            return false;
        }

        SteerFlyout.ShowAt(SteerButton);
        return true;
    }

    /// <summary>Closes the steer flyout and returns focus to the Steer button.</summary>
    internal void CloseSteer() => SteerFlyout.Hide();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(Model);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _watch.Set(Model);
        }

        SteerPanel.DataContext = Model;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !IsInsideButton(e.Source))
        {
            _press = e;
            Focus(NavigationMethod.Pointer);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_press is not { } press || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var moved = e.GetPosition(this) - press.GetPosition(this);
        if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
        {
            return;
        }

        _press = null;
        if (Model is { } model)
        {
            _ = DragAsync(press, model.Id);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _press = null;
    }

    /// <inheritdoc />
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);

        if (!IsInsideButton(e.Source))
        {
            Model?.OpenCommand.Execute(null);
        }
    }

    private static async Task DragAsync(PointerPressedEventArgs press, string sessionId)
    {
        using var transfer = TransferOf(sessionId);
        await DragDrop.DoDragDropAsync(press, transfer, DragDropEffects.Move).ConfigureAwait(true);
    }

    private static DataTransfer TransferOf(string sessionId)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(sessionId));
        return transfer;
    }

    private static bool IsInsideButton(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button)
            {
                return true;
            }
        }

        return false;
    }

    private void Refresh()
    {
        var model = Model;
        var card = model?.Card;
        var title = card?.Title ?? string.Empty;
        StatusChip.Text = card?.StatusText;
        StatusChip.IconKey = KanbanStatusVisuals.IconOf(card?.Status);
        StatusChip.Tone = KanbanStatusVisuals.ToneOf(card?.Status);
        AutomationProperties.SetName(SteerButton, KanbanCopy.SteerNameFor(title));
        AutomationProperties.SetName(SteerPanel, KanbanCopy.SteerPanelNameFor(title));

        var failed = model?.SteerFailed == true;
        SetClass(SteerResult, "tone-error", failed);
        SetClass(SteerResult, "tone-secondary", !failed);
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

    private void OnSteerOpened(object? sender, EventArgs e) => SteerBox.FocusWhenReady();

    private void OnSteerClosed(object? sender, EventArgs e) => SteerButton.FocusWhenReady();

    private void OnSteerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            CloseSteer();
        }
    }
}
