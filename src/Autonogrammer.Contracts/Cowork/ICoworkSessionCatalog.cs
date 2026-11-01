namespace Autonogrammer.Contracts.Cowork;

/// <summary>
/// The list of Cowork tasks a head shows. An implementation raises <see cref="Changed"/> after a task is added, removed or updated.
/// This is a Cowork-only surface. A Native or Web adapter implements it; the registry in this phase does not.
/// </summary>
public interface ICoworkSessionCatalog
{
    /// <summary>Occurs after the list or any task in it changed. The handler may run on any thread.</summary>
    event EventHandler? Changed;

    /// <summary>Returns the tasks, most recently updated first.</summary>
    /// <returns>A snapshot that later changes do not affect.</returns>
    IReadOnlyList<CoworkSessionSummary> Snapshot();
}
