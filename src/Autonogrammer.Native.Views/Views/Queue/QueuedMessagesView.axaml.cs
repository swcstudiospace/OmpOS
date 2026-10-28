using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Autonogrammer.Presentation.Queue;

namespace Autonogrammer.Native.Views.Queue;

/// <summary>
/// The queued messages under the composer: the Queued pill, one row for each message the user sent while the agent worked, and the polite live
/// region that says what moved, what was sent now or what failed. The view hides itself while the bound <see cref="QueuedMessagesModel"/> is empty.
/// Each row has Move up, Move down, Send now and Remove buttons, which call the model's commands with the message id; a focused row (or a button in
/// it) is also reordered with Alt+Up and Alt+Down and removed with Delete. After a move or a removal keyboard focus returns to the row, so a user
/// can keep going.
/// </summary>
internal partial class QueuedMessagesView : UserControl
{
    private string? _restoreId;
    private int _restoreIndex;
    private string? _restoreButton;

    /// <summary>Creates the view with no model. It stays visible until a model that is empty is bound.</summary>
    public QueuedMessagesView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(Button.ClickEvent, OnRowButtonClick, RoutingStrategies.Bubble);
    }

    /// <summary>Gets the text of the Queued pill.</summary>
    internal TextBlock Pill => PillText;

    /// <summary>Gets the Clear all button.</summary>
    internal Button ClearAll => ClearAllButton;

    /// <summary>Gets the list of queued messages. Its rows are in queue order, the next message to run first.</summary>
    internal ItemsControl Rows => QueueRows;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Announcement => AnnouncementText;

    private QueuedMessagesModel? Model => DataContext as QueuedMessagesModel;

    private static (string? Id, int Index) Locate(object? source) =>
        source is StyledElement { DataContext: QueuedMessage message } ? (message.Id, message.Position - 1) : (null, -1);

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || Model is not { } model)
        {
            return;
        }

        var (id, index) = Locate(e.Source);
        if (id is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up when e.KeyModifiers == KeyModifiers.Alt:
                e.Handled = true;
                if (model.MoveUp(id))
                {
                    Remember(id, index, button: null);
                }

                break;
            case Key.Down when e.KeyModifiers == KeyModifiers.Alt:
                e.Handled = true;
                if (model.MoveDown(id))
                {
                    Remember(id, index, button: null);
                }

                break;
            case Key.Delete when e.KeyModifiers == KeyModifiers.None:
                e.Handled = true;
                if (model.Remove(id))
                {
                    Remember(id, index, button: null);
                }

                break;
            default:
                break;
        }
    }

    // The click handler runs before the button runs its own action, so a button that moves or removes a message only asks for focus to be
    // restored: the request runs after the action, and does nothing when focus never left the view.
    private void OnRowButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model || e.Source is not Button button)
        {
            return;
        }

        var (id, index) = Locate(button);
        if (id is null)
        {
            return;
        }

        var action = button.Tag as string;
        switch (action)
        {
            case "MoveUp":
                Remember(id, index, action);
                model.MoveUpCommand.Execute(id);
                break;
            case "MoveDown":
                Remember(id, index, action);
                model.MoveDownCommand.Execute(id);
                break;
            case "Remove":
                Remember(id, index, button: null);
                model.RemoveCommand.Execute(id);
                break;
            case "SendNow":
                model.SendNowCommand.Execute(id);
                break;
            default:
                break;
        }

        e.Handled = true;
    }

    private void Remember(string id, int index, string? button)
    {
        _restoreId = id;
        _restoreIndex = index;
        _restoreButton = button;
        Dispatcher.UIThread.Post(RestoreFocus, DispatcherPriority.Input);
    }

    private void RestoreFocus()
    {
        var id = _restoreId;
        var button = _restoreButton;
        _restoreId = null;
        _restoreButton = null;
        if (id is null || Model is not { } model || model.Messages.Count == 0 || FocusRestsOn(id))
        {
            return;
        }

        QueueRows.UpdateLayout();
        var target = -1;
        for (var index = 0; index < model.Messages.Count; index++)
        {
            if (string.Equals(model.Messages[index].Id, id, StringComparison.Ordinal))
            {
                target = index;
                break;
            }
        }

        if (target < 0)
        {
            // The message is gone, so the row that took its place gets focus.
            target = Math.Clamp(_restoreIndex, 0, model.Messages.Count - 1);
            button = null;
        }

        if (QueueRows.ContainerFromIndex(target) is not { } container)
        {
            return;
        }

        if (button is not null
            && container.GetVisualDescendants().OfType<Button>().FirstOrDefault(candidate => string.Equals(candidate.Tag as string, button, StringComparison.Ordinal)) is { IsEnabled: true } kept)
        {
            _ = kept.Focus();
            return;
        }

        _ = container.GetVisualDescendants().OfType<Border>().FirstOrDefault(static candidate => candidate.Focusable && candidate.IsTabStop)?.Focus();
    }

    // Focus is on the message already when its container kept it. A removed container leaves focus on nothing (or on a control that left the tree), and a container that was reused for another message holds that message.
    private bool FocusRestsOn(string id) =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control { DataContext: QueuedMessage held } focused
        && TopLevel.GetTopLevel(focused) is not null
        && string.Equals(held.Id, id, StringComparison.Ordinal);
}
