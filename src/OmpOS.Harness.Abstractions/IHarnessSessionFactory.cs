namespace OmpOS.Harness.Abstractions;

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
}
#pragma warning restore CA1515
