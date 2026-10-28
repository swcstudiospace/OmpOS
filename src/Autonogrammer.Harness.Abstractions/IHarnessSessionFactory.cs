namespace Autonogrammer.Harness.Abstractions;

#pragma warning disable CA1515 // Test seam: integration tests replace this factory to script harness streams.
// (The host itself is internal-only; this single interface stays public so the test
// assembly can implement it without InternalsVisibleTo gymnastics.)
/// <summary>
/// Creates harness sessions for prompt execution. Phase 2 wires the in-memory adapter;
/// later phases add ACP/RPC adapters behind this same seam without touching endpoints.
/// </summary>
public interface IHarnessSessionFactory
{
    /// <summary>Creates a harness session that will execute <paramref name="prompt"/>.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>A new, not-yet-started harness session.</returns>
    IOmpSession CreateSession(string prompt);

    /// <summary>
    /// Creates a harness session for one run of a session, so the harness can route that run's permission requests to it.
    /// The default ignores the identity and returns <see cref="CreateSession(string)"/>, so a factory without per-run routing needs no change.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="runId">The run identifier. A permission request is decided for this run only.</param>
    /// <returns>A new, not-yet-started harness session.</returns>
    IOmpSession CreateSession(string prompt, string sessionId, string runId) => CreateSession(prompt);
}
#pragma warning restore CA1515
