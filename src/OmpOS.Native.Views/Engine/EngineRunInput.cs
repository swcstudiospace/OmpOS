using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Engine;

/// <summary>
/// The run input of the composed engine. ACP has no steer or follow-up command, so a message the active run accepts is held here in
/// send order. The window sends each held message as its own turn once the active turn ends, and drops the held messages when the user
/// stops the run or a turn faults.
/// </summary>
/// <remarks>
/// <see cref="SteerAsync"/> and <see cref="FollowUpAsync"/> both hold the message and complete at once, so neither cancels the turn in
/// flight. The scheduler and the window that owns this input both run on the UI thread, so the queue needs no lock.
/// </remarks>
internal sealed class EngineRunInput : IRunInput
{
    private readonly Queue<(string Text, bool IsSteer)> _held = new();
    private int _steersHeld;

    /// <inheritdoc/>
    public int SteersHeld => _steersHeld;

    /// <inheritdoc/>
    public Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _held.Enqueue((text, IsSteer: true));
        _steersHeld++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task FollowUpAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _held.Enqueue((text, IsSteer: false));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes and returns the oldest held message. A steer leaves <see cref="SteersHeld"/> here, because its turn is the one that starts.
    /// </summary>
    /// <returns>The next message to send, or null when no message is held.</returns>
    public string? TakeNext()
    {
        if (!_held.TryDequeue(out var held))
        {
            return null;
        }

        if (held.IsSteer)
        {
            _steersHeld--;
        }

        return held.Text;
    }

    /// <summary>Drops every held message, so none is sent.</summary>
    /// <returns>The number of messages dropped.</returns>
    public int Discard()
    {
        var count = _held.Count;
        _held.Clear();
        _steersHeld = 0;
        return count;
    }
}
