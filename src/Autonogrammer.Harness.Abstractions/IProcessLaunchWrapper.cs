namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Optional wrapper around a child process launch: names the executable and its argument vector.
/// Processes are started by argument vector only, never through a shell.
/// </summary>
public interface IProcessLaunchWrapper
{
    /// <summary>Gets the executable file name or path to start.</summary>
    string FileName { get; }

    /// <summary>Gets the argument vector passed to the executable.</summary>
    IReadOnlyList<string> Arguments { get; }
}
