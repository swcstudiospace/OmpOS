namespace Autonogrammer.Workspace;

/// <summary>
/// The outcome of a setup run.
/// </summary>
public enum SetupStatus
{
    /// <summary>Every step exited with status zero and the agent start callback was invoked.</summary>
    Passed,

    /// <summary>A step failed or the setup file was rejected. The agent start callback was not invoked.</summary>
    Failed,

    /// <summary>
    /// The user has not approved the repository's setup file, or refused it. No step ran and the agent start callback was
    /// not invoked.
    /// </summary>
    ConsentRefused,
}
