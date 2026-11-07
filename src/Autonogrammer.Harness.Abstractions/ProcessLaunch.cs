namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// One process start: an executable, an argument vector, an optional working directory and an environment.
/// A null environment value means the variable is unset. Processes are started by argument vector only, never through a shell.
/// </summary>
/// <param name="FileName">The executable file name or path to start.</param>
/// <param name="Arguments">The argument vector, one element each and in order.</param>
/// <param name="WorkingDirectory">The working directory, or <see langword="null"/> to inherit the current one.</param>
/// <param name="Environment">The environment. A null value unsets the variable.</param>
public sealed record ProcessLaunch(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);
