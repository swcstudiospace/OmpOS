namespace Autonogrammer.Workspace;

/// <summary>
/// The single entry point a head or service calls before it starts an agent run. It wraps <see cref="SetupRunner"/>:
/// the consent store is consulted before any step runs, a failed or refused setup becomes a typed
/// <see cref="SetupGateFailure"/> carrying the reason to show, no run is started in that case, and the variables of a
/// passing setup are handed to the run start callback and returned for the run environment.
/// </summary>
public static class SetupRunGate
{
    private const string UnknownFailure = "the setup run failed without a reason";

    /// <summary>
    /// Runs the workspace setup and, only when it passes, invokes <paramref name="startRun"/> with the setup environment.
    /// </summary>
    /// <param name="root">The workspace root.</param>
    /// <param name="consentStore">Remembers approvals per repository path and setup file hash. Consulted first.</param>
    /// <param name="consentPrompt">Asks the user when no approval is recorded.</param>
    /// <param name="startRun">
    /// Starts the agent run with the variables the setup declared. It is never invoked when the setup fails or is refused.
    /// Its completion is the completion of this call.
    /// </param>
    /// <param name="cancellationToken">Cancels the question, the current step, or the run.</param>
    /// <returns>
    /// A started result with the run environment, or a stopped result whose <see cref="SetupGateFailure.FailureReason"/>
    /// is the reason to show the user.
    /// </returns>
    public static async Task<SetupGateResult> RunAsync(
        WorkspaceRoot root,
        ISetupConsentStore consentStore,
        ISetupConsentPrompt consentPrompt,
        Func<IReadOnlyDictionary<string, string>, CancellationToken, Task> startRun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(consentStore);
        ArgumentNullException.ThrowIfNull(consentPrompt);
        ArgumentNullException.ThrowIfNull(startRun);

        var result = await SetupRunner.RunAsync(root, consentStore, consentPrompt, startRun, cancellationToken)
            .ConfigureAwait(false);

        return result.Status == SetupStatus.Passed
            ? SetupGateResult.Started(result.Environment)
            : SetupGateResult.Stopped(new SetupGateFailure(
                result.Status,
                result.FailedStepIndex,
                string.IsNullOrWhiteSpace(result.FailureReason) ? UnknownFailure : result.FailureReason));
    }
}
