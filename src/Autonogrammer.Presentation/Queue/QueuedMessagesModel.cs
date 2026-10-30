using System.Collections.ObjectModel;
using System.Globalization;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;
using Autonogrammer.Presentation.Runs;

namespace Autonogrammer.Presentation.Queue;

/// <summary>
/// The messages a user sent while the agent was working, kept in order until the agent's turn ends (Cursor's Queued pill). The user can reorder them, edit one, remove one, or send one now,
/// which delivers it into the running turn instead of waiting. The queue lives in memory, one per session (<see cref="QueuedMessagesRegistry"/>), holds at most
/// <see cref="RunInputScheduler.MaxQueuedMessages"/> messages like the scheduler's own queue, and never loses a message: a message leaves the queue only after it was delivered.
/// </summary>
/// <remarks>
/// <para>The model does not talk to the engine. <see cref="QueuedMessagesSteeringAdapter"/> joins it to <see cref="SteeringInputModel"/> and to the head's way of starting a turn.</para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class QueuedMessagesModel : ObservableModel
{
    private readonly TimeProvider _timeProvider;
    private readonly ObservableCollection<QueuedMessage> _messages = [];
    private readonly AgentPanelCommand _removeCommand;
    private readonly AgentPanelCommand _moveUpCommand;
    private readonly AgentPanelCommand _moveDownCommand;
    private readonly AgentPanelCommand _sendNowCommand;
    private readonly AgentPanelCommand _clearCommand;
    private int _number;
    private bool _isSending;
    private string _announcement = string.Empty;

    /// <summary>Creates the queue of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="timeProvider">The clock of the queued times.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public QueuedMessagesModel(string sessionId, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(timeProvider);

        SessionId = sessionId;
        _timeProvider = timeProvider;
        Messages = new ReadOnlyObservableCollection<QueuedMessage>(_messages);
        _removeCommand = new AgentPanelCommand(parameter => { Remove(Id(parameter)); return Task.CompletedTask; }, Ignore, parameter => Id(parameter).Length > 0);
        _moveUpCommand = new AgentPanelCommand(parameter => { MoveUp(Id(parameter)); return Task.CompletedTask; }, Ignore, parameter => Id(parameter).Length > 0);
        _moveDownCommand = new AgentPanelCommand(parameter => { MoveDown(Id(parameter)); return Task.CompletedTask; }, Ignore, parameter => Id(parameter).Length > 0);
        _sendNowCommand = new AgentPanelCommand(parameter => SendNowAsync(Id(parameter), CancellationToken.None), Fail, parameter => !_isSending && Id(parameter).Length > 0);
        _clearCommand = new AgentPanelCommand(_ => { Clear(); return Task.CompletedTask; }, Ignore, _ => _messages.Count > 0);
    }

    /// <summary>Gets the session the queue belongs to.</summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets or sets what delivers a message into the running turn and tells how it went. <see cref="QueuedMessagesSteeringAdapter"/> sets it. The message stays in the queue while it is called and leaves only on <see cref="QueueSendOutcome.Sent"/>. Null means this queue cannot send now: <see cref="SendNowAsync"/> then reports <see cref="QueueSendOutcome.Failed"/>.
    /// </summary>
    public Func<string, CancellationToken, Task<QueueSendOutcome>>? Sender { get; set; }

    /// <summary>Gets the queued messages. The first is the next to run.</summary>
    public ReadOnlyObservableCollection<QueuedMessage> Messages { get; }

    /// <summary>Gets the number of queued messages.</summary>
    public int Count => _messages.Count;

    /// <summary>Gets whether nothing is queued. The Queued pill is hidden while it is true.</summary>
    public bool IsEmpty => _messages.Count == 0;

    /// <summary>Gets whether the queue holds the most messages it may.</summary>
    public bool IsFull => _messages.Count >= RunInputScheduler.MaxQueuedMessages;

    /// <summary>Gets the pill text: the number queued in words, or an empty string while the queue is empty.</summary>
    public string PillText => IsEmpty ? string.Empty : QueueCopy.Count(_messages.Count);

    /// <summary>Gets the sentence shown while the queue is empty, or an empty string.</summary>
    public string EmptyText => IsEmpty ? QueueCopy.Empty : string.Empty;

    /// <summary>Gets the last sentence for assistive technology: what moved, what was sent, what failed.</summary>
    public string Announcement => _announcement;

    /// <summary>Gets the command that removes a message. Its parameter is the message's id.</summary>
    public AgentPanelCommand RemoveCommand => _removeCommand;

    /// <summary>Gets the command that moves a message up. Its parameter is the message's id.</summary>
    public AgentPanelCommand MoveUpCommand => _moveUpCommand;

    /// <summary>Gets the command that moves a message down. Its parameter is the message's id.</summary>
    public AgentPanelCommand MoveDownCommand => _moveDownCommand;

    /// <summary>Gets the command that sends a message into the running turn now. Its parameter is the message's id.</summary>
    public AgentPanelCommand SendNowCommand => _sendNowCommand;

    /// <summary>Gets the command that empties the queue.</summary>
    public AgentPanelCommand ClearCommand => _clearCommand;

    /// <summary>Adds a message at the end of the queue.</summary>
    /// <param name="text">The message.</param>
    /// <returns>Whether it was queued. A blank text is ignored and a full queue refuses; the announcement says so.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public QueueOutcome Enqueue(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return QueueOutcome.Ignored;
        }

        if (IsFull)
        {
            Announce(QueueCopy.Full(RunInputScheduler.MaxQueuedMessages));
            return QueueOutcome.Full;
        }

        _number++;
        _messages.Add(new QueuedMessage(string.Create(CultureInfo.InvariantCulture, $"queued-{_number}"), text, _timeProvider.GetUtcNow()));
        Renumber();
        Announce(QueueCopy.Count(_messages.Count) + ".");
        return QueueOutcome.Queued;
    }

    /// <summary>Removes a message.</summary>
    /// <param name="id">The message's id.</param>
    /// <returns><see langword="false"/> when the queue has no such message.</returns>
    public bool Remove(string id)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return false;
        }

        _messages.RemoveAt(index);
        Renumber();
        Announce(QueueCopy.Count(_messages.Count) + ".");
        return true;
    }

    /// <summary>Empties the queue. The owner of the run calls it when the user stops the run.</summary>
    /// <returns>The number of messages dropped.</returns>
    public int Clear()
    {
        var count = _messages.Count;
        _messages.Clear();
        Renumber();
        if (count > 0)
        {
            Announce(QueueCopy.Cleared);
        }

        return count;
    }

    /// <summary>Replaces the text of a message. Its place in the queue stays.</summary>
    /// <param name="id">The message's id.</param>
    /// <param name="text">The new text.</param>
    /// <returns><see langword="false"/> when the queue has no such message or the new text is blank (use <see cref="Remove"/> to drop a message).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public bool Edit(string id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var index = IndexOf(id);
        if (index < 0 || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _messages[index].SetText(text);
        return true;
    }

    /// <summary>Moves a message one place towards the front.</summary>
    /// <param name="id">The message's id.</param>
    /// <returns><see langword="false"/> when the message is not in the queue or is already first.</returns>
    public bool MoveUp(string id) => MoveBy(id, -1);

    /// <summary>Moves a message one place towards the back.</summary>
    /// <param name="id">The message's id.</param>
    /// <returns><see langword="false"/> when the message is not in the queue or is already last.</returns>
    public bool MoveDown(string id) => MoveBy(id, 1);

    /// <summary>Moves a message to a place, for a drag and drop. A place outside the queue is clamped to its ends.</summary>
    /// <param name="id">The message's id.</param>
    /// <param name="position">The place the message gets, from 1.</param>
    /// <returns><see langword="false"/> when the message is not in the queue or already is at that place.</returns>
    public bool MoveTo(string id, int position)
    {
        var from = IndexOf(id);
        if (from < 0)
        {
            return false;
        }

        var to = Math.Clamp(position - 1, 0, _messages.Count - 1);
        if (to == from)
        {
            return false;
        }

        _messages.Move(from, to);
        Renumber();
        Announce(QueueCopy.Moved(from + 1, to + 1, _messages.Count));
        return true;
    }

    /// <summary>Takes the next message out of the queue, to start it as the next turn.</summary>
    /// <returns>The message, or <see langword="null"/> when the queue is empty.</returns>
    public QueuedMessage? TakeNext()
    {
        if (_messages.Count == 0)
        {
            return null;
        }

        var next = _messages[0];
        _messages.RemoveAt(0);
        Renumber();
        return next;
    }

    /// <summary>Puts a message that was taken back at the front, because starting its turn failed.</summary>
    /// <param name="message">The message <see cref="TakeNext"/> returned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    public void PutBack(QueuedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (IndexOf(message.Id) >= 0)
        {
            return;
        }

        _messages.Insert(0, message);
        Renumber();
    }

    /// <summary>Sends a queued message into the running turn now instead of waiting for the turn to end.</summary>
    /// <param name="id">The message's id.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>How it went. Only <see cref="QueueSendOutcome.Sent"/> removes the message from the queue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    public async Task<QueueSendOutcome> SendNowAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var index = IndexOf(id);
        if (index < 0)
        {
            return QueueSendOutcome.NotFound;
        }

        var sender = Sender;
        if (sender is null || _isSending)
        {
            Announce(QueueCopy.SendFailed(_isSending ? "another message is being sent" : "this window cannot send into a running turn"));
            return QueueSendOutcome.Failed;
        }

        var message = _messages[index];
        SetSending(true);
        QueueSendOutcome outcome;
        try
        {
            outcome = await sender(message.Text, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Announce(QueueCopy.SendFailed(exception.Message));
            return QueueSendOutcome.Failed;
        }
        finally
        {
            SetSending(false);
        }

        switch (outcome)
        {
            case QueueSendOutcome.Sent:
                var place = IndexOf(id);
                if (place >= 0)
                {
                    _messages.RemoveAt(place);
                    Renumber();
                }

                Announce(QueueCopy.SentNow(index + 1));
                break;
            case QueueSendOutcome.NoActiveRun:
                Announce(QueueCopy.NoRun);
                break;
            default:
                Announce(QueueCopy.SendFailed("the agent did not take it"));
                break;
        }

        return outcome;
    }

    private static string Id(object? parameter) => parameter as string ?? string.Empty;

    private void Ignore(Exception exception) => Announce(exception.Message);

    private void Fail(Exception exception) => Announce(QueueCopy.SendFailed(exception.Message));

    private int IndexOf(string id)
    {
        for (var index = 0; index < _messages.Count; index++)
        {
            if (string.Equals(_messages[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private bool MoveBy(string id, int step)
    {
        var from = IndexOf(id);
        var to = from + step;
        if (from < 0 || to < 0 || to >= _messages.Count)
        {
            return false;
        }

        _messages.Move(from, to);
        Renumber();
        Announce(QueueCopy.Moved(from + 1, to + 1, _messages.Count));
        return true;
    }

    private void Renumber()
    {
        for (var index = 0; index < _messages.Count; index++)
        {
            _messages[index].SetPlace(index + 1, _messages.Count);
        }

        NotifyPropertyChanged(nameof(Count));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(IsFull));
        NotifyPropertyChanged(nameof(PillText));
        NotifyPropertyChanged(nameof(EmptyText));
        _clearCommand.NotifyCanExecuteChanged();
    }

    private void SetSending(bool sending)
    {
        _isSending = sending;
        _sendNowCommand.NotifyCanExecuteChanged();
    }

    private void Announce(string text) => SetProperty(ref _announcement, text, nameof(Announcement));
}
