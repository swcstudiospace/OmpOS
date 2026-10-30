namespace Autonogrammer.Presentation.Changes;

/// <summary>The result of one action on one changed file. An expected failure is a result, never an exception.</summary>
/// <param name="Path">The workspace-relative path of the file.</param>
/// <param name="Outcome">What the action did.</param>
/// <param name="Message">One sentence that says it, for the status line and assistive technology.</param>
public sealed record ChangeResult(string Path, ChangeOutcome Outcome, string Message);
