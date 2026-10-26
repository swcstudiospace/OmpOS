namespace OmpOS.Workspace;

/// <summary>
/// What a head shows the user when it asks whether a repository's setup steps may run.
/// </summary>
/// <param name="Key">The repository path and setup file hash that an approval would be recorded for.</param>
/// <param name="SetupFile">The setup file, relative to the repository root, in portable form.</param>
/// <param name="Setup">The validated setup: the commands that would run and the environment variables that would be set.</param>
public sealed record SetupConsentRequest(SetupConsentKey Key, string SetupFile, WorkspaceSetup Setup);
