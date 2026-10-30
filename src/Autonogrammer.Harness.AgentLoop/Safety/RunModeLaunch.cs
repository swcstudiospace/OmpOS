namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>How omp is started for a run mode: the value of its <c>--approval-mode</c> flag and what that means for Autonogrammer.</summary>
/// <param name="Mode">The run mode.</param>
/// <param name="ApprovalMode">The value for <c>omp acp --approval-mode</c>: <c>always-ask</c>, <c>always-ask-cached</c>, <c>write</c> or <c>yolo</c>.</param>
/// <param name="OmpAsksAutonogrammer">True when omp sends Autonogrammer the calls that need a decision. False for <c>yolo</c>: omp sends none, so only omp's own <c>bash.patterns</c> deny rules apply.</param>
public sealed record RunModeLaunch(RunMode Mode, string ApprovalMode, bool OmpAsksAutonogrammer)
{
    /// <summary>Gets the arguments to add to the <c>omp acp</c> command line.</summary>
    public IReadOnlyList<string> Arguments => ["--approval-mode", ApprovalMode];
}
