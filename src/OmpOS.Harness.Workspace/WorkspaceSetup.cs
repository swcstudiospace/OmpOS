namespace OmpOS.Workspace;

/// <summary>
/// A validated setup file: ordered steps and the environment shared by every step and by the agent run.
/// </summary>
/// <param name="Steps">The steps, in the order they run.</param>
/// <param name="Environment">The environment variables added to each step and returned to the run.</param>
public sealed record WorkspaceSetup(IReadOnlyList<SetupStep> Steps, IReadOnlyDictionary<string, string> Environment)
{
    /// <summary>
    /// Gets the setup used when the workspace has no setup file: no steps and no variables.
    /// </summary>
    public static WorkspaceSetup Empty { get; } = new([], new Dictionary<string, string>(StringComparer.Ordinal));
}
