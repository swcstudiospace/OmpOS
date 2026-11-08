namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// The result of one Omp check or install of a remote environment. <c>PlannedActions</c> are the lines an install applies, in order. Install Omp
/// applies exactly these, and only when <c>Outcome</c> is <see cref="RemoteOmpOutcome.Planned"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Drift">What differs on the machine, with each item's reason.</param>
/// <param name="PlannedActions">The lines an install applies, one per action, in run order.</param>
/// <param name="Reason">Why the outcome is a refusal, a failure or unavailable, in one plain sentence; otherwise <see langword="null"/>.</param>
/// <param name="Changed">Whether an install may have written to the machine.</param>
public sealed record RemoteOmpReport(
    RemoteOmpOutcome Outcome,
    IReadOnlyList<RemoteOmpDriftLine> Drift,
    IReadOnlyList<string> PlannedActions,
    string? Reason,
    bool Changed);
