using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Presentation.Queue;

/// <summary>
/// Joins <see cref="QueuedMessagesModel"/> to <see cref="SteeringInputModel"/> and to the head's way of starting a turn, without changing either. A message queued here is held by the queue model
/// and is not given to the run scheduler's own follow-up queue, so it can be reordered, edited and removed; use one path or the other for a given message. <see cref="SendNowAsync"/>
/// hands one message to the steering input as a steer, and <see cref="DispatchNextAsync"/> starts the first message as a new turn when the run ends.
/// </summary>
public sealed class QueuedMessagesSteeringAdapter
{
    private readonly QueuedMessagesModel _queue;
    private readonly SteeringInputModel _steering;
    private readonly Func<string, CancellationToken, Task> _startTurn;

    /// <summary>Creates the adapter of one session.</summary>
    /// <param name="queue">The session's queue.</param>
    /// <param name="steering">The session's steering input. Its <c>SubmitAsync</c> delivers a steer into the running turn.</param>
    /// <param name="startTurn">Starts a message as a new prompt after the run ended. It throws when the prompt was not accepted; the message then returns to the front of the queue.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public QueuedMessagesSteeringAdapter(QueuedMessagesModel queue, SteeringInputModel steering, Func<string, CancellationToken, Task> startTurn)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(steering);
        ArgumentNullException.ThrowIfNull(startTurn);
        _queue = queue;
        _steering = steering;
        _startTurn = startTurn;
        _queue.Sender = SendTextNowAsync;
    }

    /// <summary>Gets the queue the adapter works on.</summary>
    public QueuedMessagesModel Queue => _queue;

    /// <summary>Maps the steering input's outcome to the queue's: accepted is sent, no active run means the turn ended, anything else is a failure.</summary>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The outcome. The constructor sets this method as the <see cref="QueuedMessagesModel.Sender"/> of the queue.</returns>
    public async Task<QueueSendOutcome> SendTextNowAsync(string text, CancellationToken cancellationToken)
    {
        var outcome = await _steering.SubmitAsync(text, SteeringMode.Steer, cancellationToken).ConfigureAwait(true);
        return outcome switch
        {
            SteeringSubmitOutcome.Accepted => QueueSendOutcome.Sent,
            SteeringSubmitOutcome.NoActiveRun => QueueSendOutcome.NoActiveRun,
            _ => QueueSendOutcome.Failed,
        };
    }

    /// <summary>Sends one queued message into the running turn now.</summary>
    /// <param name="id">The message's id.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>How it went. When no run is active any more the message stays queued and the head calls <see cref="DispatchNextAsync"/>.</returns>
    public Task<QueueSendOutcome> SendNowAsync(string id, CancellationToken cancellationToken) => _queue.SendNowAsync(id, cancellationToken);

    /// <summary>Starts the first queued message as a new turn. The head calls it when a run completes.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns><see langword="true"/> when a message was started; <see langword="false"/> when the queue was empty or the start failed (the message is back at the front).</returns>
    public async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
    {
        var next = _queue.TakeNext();
        if (next is null)
        {
            return false;
        }

        try
        {
            await _startTurn(next.Text, cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            _queue.PutBack(next);
            return false;
        }
        catch (OperationCanceledException)
        {
            _queue.PutBack(next);
            throw;
        }
    }
}
