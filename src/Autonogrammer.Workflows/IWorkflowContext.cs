namespace Autonogrammer.Workflows;

/// <summary>
/// Workflow context providing deterministic primitives for workflow execution.
/// </summary>
/// <remarks>
/// Implementations must provide deterministic behavior suitable for replay.
/// The direct runner uses a real-time implementation.
/// </remarks>
public interface IWorkflowContext
{
    /// <summary>Gets the current UTC time (deterministic in replay).</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Generates a new deterministic GUID.</summary>
    Guid NewGuid();

    /// <summary>Gets a deterministic random number generator.</summary>
    Random Random { get; }

    /// <summary>Delays execution deterministically.</summary>
    /// <param name="delay">The delay duration.</param>
    /// <param name="cancellationToken">Cancels the delay.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);

    /// <summary>Records a heartbeat payload for long-running activities.</summary>
    /// <param name="details">Heartbeat details (e.g., last processed sequence number).</param>
    void Heartbeat(string details);
}
