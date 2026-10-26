namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Implemented by an <see cref="IOmpSession"/> (or a host over one) whose engine takes a message into the turn that is running, at
/// the engine's own tool-batch and model-call boundaries. The engine owns the delivery: this surface only asks and reports what the engine answered.
/// </summary>
public interface IOmpLiveSteering
{
    /// <summary>Gets a value indicating whether the engine offers live steering for the session. It is <see langword="false"/> until the session is open.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Puts <paramref name="text"/> into the turn that is running. A request the session cannot take (no open session, no running turn,
    /// an engine without live steering, a blank or too long text) is refused without sending anything and reported in the result.
    /// </summary>
    /// <param name="text">The message. It is 1 to 8192 UTF-8 bytes and not blank.</param>
    /// <param name="queue">The queue to put the message into.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the attempt ended.</returns>
    Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken);
}
