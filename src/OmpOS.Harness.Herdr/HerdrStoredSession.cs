namespace OmpOS.Harness.Herdr;

/// <summary>
/// The local record for one herdr session, as the <see cref="HerdrSessionStore"/> persists it: the session id, the provider
/// selection, the native reference, the last-known state and the last sequence. The sequence is kept so a restart can attach
/// by stored id from the last point seen.
/// </summary>
/// <param name="Id">The session identifier.</param>
/// <param name="ProviderId">The provider selection, as a plain provider identifier.</param>
/// <param name="NativeReference">The omp session reference, or null when the agent has not reported one.</param>
/// <param name="LastKnownState">The last state the server reported for the session.</param>
/// <param name="LastSequence">The last sequence number the server reported for the session.</param>
public sealed record HerdrStoredSession(
    HerdrSessionId Id,
    string ProviderId,
    NativeSessionReference? NativeReference,
    HerdrSessionState LastKnownState,
    long LastSequence);
