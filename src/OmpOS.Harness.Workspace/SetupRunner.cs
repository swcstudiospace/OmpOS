namespace OmpOS.Workspace;

/// <summary>
/// Runs the workspace setup file before an agent run. The agent is started only when every step passes, and the
/// environment the setup declares is the environment the run receives.
/// </summary>
public static class SetupRunner
{
    /// <summary>
    /// Runs every setup step in order, then invokes <paramref name="startAgent"/> with the declared environment.
    /// A failed or rejected setup returns a failed result and never invokes <paramref name="startAgent"/>.
    /// A workspace without a setup file runs the agent with an empty environment.
    /// </summary>
    /// <param name="root">The workspace root. Step working directories are resolved against it.</param>
    /// <param name="startAgent">
    /// Starts the agent run with the environment. Its completion is the completion of this call.
    /// </param>
    /// <param name="cancellationToken">Cancels the current step, or the agent run.</param>
    /// <returns>The outcome of the setup run.</returns>
    public static async Task<SetupRunResult> RunAsync(
        WorkspaceRoot root,
        Func<IReadOnlyDictionary<string, string>, CancellationToken, Task> startAgent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(startAgent);

        var (setup, diagnostic) = SetupFileReader.Read(root);
        if (diagnostic is not null)
        {
            return SetupRunResult.Failed(
                null,
                $"setup file '{diagnostic.RelativePath}' is rejected: {diagnostic.Reason}");
        }

        var effective = setup ?? WorkspaceSetup.Empty;
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
