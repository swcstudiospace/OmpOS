namespace OmpOS.Workspace.Checkpoints;

/// <summary>The result of a rewind. An expected failure is a status, never an exception.</summary>
/// <param name="Status">How the rewind ended.</param>
/// <param name="Plan">What was planned, with the state of the files when the rewind started.</param>
/// <param name="Written">The paths that were written or deleted, relative to their folder; empty unless <paramref name="Status"/> is <see cref="RewindStatus.Completed"/>.</param>
/// <param name="Message">One sentence that says what happened.</param>
public sealed record RewindResult(RewindStatus Status, RewindPlan Plan, IReadOnlyList<string> Written, string Message);
