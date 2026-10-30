namespace Autonogrammer.Workspace;

/// <summary>
/// One validated setup step.
/// </summary>
/// <param name="Command">The executable. A bare name is resolved through PATH; an absolute path is used as given;
/// a relative path with a directory part is resolved inside the workspace root.</param>
/// <param name="Arguments">The argument list, passed without a shell.</param>
/// <param name="WorkingDirectory">The working directory, relative to the workspace root. "." is the root.</param>
public sealed record SetupStep(string Command, IReadOnlyList<string> Arguments, string WorkingDirectory);
