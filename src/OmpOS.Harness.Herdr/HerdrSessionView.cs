namespace OmpOS.Harness.Herdr;

/// <summary>
/// The service's view of one session: the server snapshot, the provider selection from the local store, and the stale flag.
/// </summary>
/// <param name="Snapshot">The server snapshot. Its native reference falls back to the stored one when the server has not reported it.</param>
/// <param name="ProviderId">The provider selection recorded when the session was created.</param>
/// <param name="IsStale">
/// True when the session has been silent for <see cref="HerdrHeartbeatMonitor.StaleAfter"/>. It is independent of the state.
/// </param>
public sealed record HerdrSessionView(HerdrSessionSnapshot Snapshot, string ProviderId, bool IsStale);
