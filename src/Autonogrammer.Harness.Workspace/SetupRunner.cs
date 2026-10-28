namespace Autonogrammer.Workspace;

/// <summary>
/// Runs the workspace setup file before an agent run. The agent is started only when every step passes, and the
/// environment the setup declares is the environment the run receives. A setup file that declares steps or environment
/// variables runs only after the user approved it for this repository: the approval is keyed by the repository path and
/// the SHA-256 of the setup file bytes, so a changed file asks again.
/// </summary>
public static class SetupRunner
{
    /// <summary>
    /// Checks consent, runs every setup step in order, then invokes <paramref name="startAgent"/> with the declared environment.
    /// A failed or rejected setup returns a failed result, and a setup without consent returns a refused result; neither
    /// invokes <paramref name="startAgent"/>. A workspace without a setup file, or with a setup file that declares nothing
    /// to run, starts the agent with an empty environment and asks nothing.
    /// </summary>
    /// <param name="root">The workspace root. Step working directories are resolved against it.</param>
    /// <param name="consentStore">Remembers approvals per repository path and setup file hash.</param>
    /// <param name="consentPrompt">Asks the user when no approval is recorded.</param>
    /// <param name="startAgent">
    /// Starts the agent run with the environment. Its completion is the completion of this call.
    /// </param>
    /// <param name="cancellationToken">Cancels the question, the current step, or the agent run.</param>
    /// <returns>The outcome of the setup run.</returns>
    public static async Task<SetupRunResult> RunAsync(
        WorkspaceRoot root,
        ISetupConsentStore consentStore,
        ISetupConsentPrompt consentPrompt,
        Func<IReadOnlyDictionary<string, string>, CancellationToken, Task> startAgent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(consentStore);
        ArgumentNullException.ThrowIfNull(consentPrompt);
        ArgumentNullException.ThrowIfNull(startAgent);

        var (setup, diagnostic, setupHash) = SetupFileReader.Read(root);
        if (diagnostic is not null)
        {
            return SetupRunResult.Failed(
                null,
                $"setup file '{diagnostic.RelativePath}' is rejected: {diagnostic.Reason}");
        }

        var effective = setup ?? WorkspaceSetup.Empty;
        if (setup is not null && (setup.Steps.Count > 0 || setup.Environment.Count > 0))
        {
            var refusal = await RequireConsentAsync(root, setup, setupHash!, consentStore, consentPrompt, cancellationToken)
                .ConfigureAwait(false);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        for (var index = 0; index < effective.Steps.Count; index++)
        {
            var step = effective.Steps[index];
            var execution = await RunStepAsync(root, step, effective.Environment, cancellationToken)
                .ConfigureAwait(false);
            if (!execution.Succeeded)
            {
                return SetupRunResult.Failed(
                    index,
                    $"setup step {index + 1} ('{step.Command}') failed: {execution.Failure}");
            }
        }

        await startAgent(effective.Environment, cancellationToken).ConfigureAwait(false);
        return SetupRunResult.Passed(effective.Environment);
    }

    private static async Task<SetupRunResult?> RequireConsentAsync(
        WorkspaceRoot root,
        WorkspaceSetup setup,
        string setupHash,
        ISetupConsentStore consentStore,
        ISetupConsentPrompt consentPrompt,
        CancellationToken cancellationToken)
    {
        var key = new SetupConsentKey(root.FullPath, setupHash);
        if (await consentStore.IsGrantedAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var request = new SetupConsentRequest(key, WorkspaceConventions.SetupFile, setup);
        var decision = await consentPrompt.AskAsync(request, cancellationToken).ConfigureAwait(false);
        if (!decision.IsGranted)
        {
            return SetupRunResult.Refused(
                $"setup file '{WorkspaceConventions.SetupFile}' is not approved: {decision.Reason ?? "the user refused"}");
        }

        try
        {
            await consentStore.RecordGrantAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return SetupRunResult.Failed(null, $"the approval of setup file '{WorkspaceConventions.SetupFile}' could not be saved: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return SetupRunResult.Failed(null, $"the approval of setup file '{WorkspaceConventions.SetupFile}' could not be saved: {exception.Message}");
        }

        return null;
    }

    private static Task<SetupProcess.StepExecution> RunStepAsync(
        WorkspaceRoot root,
        SetupStep step,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        string executable;
        string workingDirectory;
        try
        {
            executable = ResolveExecutable(root, step.Command);
            workingDirectory = root.Resolve(step.WorkingDirectory);
        }
        catch (WorkspacePathException exception)
        {
            return Task.FromResult(SetupProcess.StepExecution.Failed(exception.Message));
        }

        if (!Directory.Exists(workingDirectory))
        {
            return Task.FromResult(SetupProcess.StepExecution.Failed(
                $"working directory '{step.WorkingDirectory}' does not exist"));
        }

        return SetupProcess.RunAsync(executable, step.Arguments, workingDirectory, environment, cancellationToken);
    }

    private static string ResolveExecutable(WorkspaceRoot root, string command) =>
        SetupFileReader.IsPathCommand(command) && !Path.IsPathRooted(command)
            ? root.Resolve(command)
            : command;
}
