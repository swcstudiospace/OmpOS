namespace OmpOS.Harness.Herdr;

/// <summary>
/// One liveness beat from the server for a session it holds alive: a running or paused session.
/// </summary>
/// <param name="Id">The session that beat.</param>
/// <param name="State">The state the server reported at the beat.</param>
/// <param name="LastSequence">The session's last sequence number at the beat.</param>
public sealed record HerdrHeartbeat(HerdrSessionId Id, HerdrSessionState State, long LastSequence);
