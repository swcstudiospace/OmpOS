namespace OmpOS.Harness.Abstractions;

/// <summary>The result of an attempt to switch the model of a session.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="CurrentModelId">The model the session uses after the attempt, or <see langword="null"/> when the engine reports none.</param>
/// <param name="Message">A plain sentence that says why the attempt did not switch the model, or <see langword="null"/> after a switch.</param>
public sealed record OmpModelSwitchResult(OmpModelSwitchOutcome Outcome, string? CurrentModelId, string? Message)
{
    /// <summary>Gets a value indicating whether the engine confirmed the requested model.</summary>
    public bool IsSwitched => Outcome == OmpModelSwitchOutcome.Switched;
}
