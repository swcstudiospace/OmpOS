namespace Autonogrammer.Harness.Agent;

/// <summary>
/// The receiving end of one agent session's stream. It accepts an event only when its sequence is greater than the
/// highest sequence accepted so far, so duplicate and out-of-order deliveries are dropped.
/// </summary>
public sealed class OmpEventReceiver
{
    private readonly string _sessionId;

    /// <summary>Initializes a new instance of the <see cref="OmpEventReceiver"/> class for one agent session.</summary>
    /// <param name="sessionId">The session whose events the receiver accepts.</param>
    public OmpEventReceiver(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _sessionId = sessionId;
    }

    /// <summary>Gets the highest sequence accepted, or 0 before the first accepted event.</summary>
    public long HighestAccepted { get; private set; }

    /// <summary>Accepts an event when its sequence is greater than every sequence accepted so far.</summary>
    /// <param name="agentEvent">The delivered event.</param>
    /// <returns>
    /// <see langword="true"/> when the event was accepted; <see langword="false"/> when it is a duplicate or older than the
    /// highest accepted sequence.
    /// </returns>
    /// <exception cref="ArgumentException">The event belongs to another session.</exception>
    /// <remarks>
    /// A sequence that is not greater than the highest accepted one is refused for good. After sequence 4 is accepted,
    /// sequences 2 and 3 are refused even though they were never accepted, because the receiver keeps no gap buffer.
    /// Duplicates return <see langword="false"/>. An event of another session throws and is not counted.
    /// </remarks>
    public bool Accept(OmpEvent agentEvent)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);
        if (!string.Equals(agentEvent.SessionId, _sessionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The event belongs to session '{agentEvent.SessionId}', not to '{_sessionId}'.",
                nameof(agentEvent));
        }

        if (agentEvent.Sequence <= HighestAccepted)
        {
            return false;
        }

        HighestAccepted = agentEvent.Sequence;
        return true;
    }
}
