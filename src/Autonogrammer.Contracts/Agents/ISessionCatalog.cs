namespace Autonogrammer.Contracts.Agents;

/// <summary>
/// The list of agent sessions the application knows about. An implementation raises <see cref="Changed"/> after any
/// session is added, removed or updated.
/// </summary>
public interface ISessionCatalog
{
    /// <summary>Occurs after the list of sessions or any session in it changed. The handler may be invoked on any thread.</summary>
    event EventHandler? Changed;

    /// <summary>Returns the sessions, most recently active first.</summary>
    /// <returns>A snapshot that later changes do not affect.</returns>
    IReadOnlyList<SessionSummary> Snapshot();
}
