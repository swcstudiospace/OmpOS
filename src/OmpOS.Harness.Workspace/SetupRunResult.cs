namespace OmpOS.Workspace;

/// <summary>
/// The result of <see cref="SetupRunner.RunAsync"/>.
/// </summary>
/// <param name="Status">Whether the run passed.</param>
/// <param name="Environment">The environment the run used. Empty when the run failed.</param>
/// <param name="FailedStepIndex">The zero-based index of the failed step, or null when the failure was not a step.</param>
/// <param name="FailureReason">Why the run failed, or null when it passed.</param>
public sealed record SetupRunResult(
    SetupStatus Status,
    IReadOnlyDictionary<string, string> Environment,
    int? FailedStepIndex,
    string? FailureReason)
{
    /// <summary>
    /// Creates a passed result.
    /// </summary>
    /// <param name="environment">The environment the run used.</param>
    public static SetupRunResult Passed(IReadOnlyDictionary<string, string> environment) =>
        new(SetupStatus.Passed, environment, null, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="failedStepIndex">The zero-based index of the failed step, or null.</param>
    /// <param name="failureReason">Why the run failed.</param>
    public static SetupRunResult Failed(int? failedStepIndex, string failureReason) =>
        new(SetupStatus.Failed, new Dictionary<string, string>(StringComparer.Ordinal), failedStepIndex, failureReason);
}
