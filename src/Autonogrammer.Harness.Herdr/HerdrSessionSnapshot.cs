namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// The server's view of one session at one point in its sequence.
/// </summary>
/// <param name="Id">The session identifier.</param>
/// <param name="State">The lifecycle state.</param>
/// <param name="IsAttached">True when a client is attached. Detaching a client never stops the agent.</param>
/// <param name="NativeReference">The omp session reference, or null when the agent has not reported one.</param>
/// <param name="LastSequence">The sequence number of the last change to this session.</param>
public sealed record HerdrSessionSnapshot(
    HerdrSessionId Id,
    HerdrSessionState State,
    bool IsAttached,
    NativeSessionReference? NativeReference,
    long LastSequence);
