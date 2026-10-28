namespace Autonogrammer.Sandbox;

/// <summary>
/// Specification for a process to run.
/// </summary>
public sealed record ProcessSpec
{
    /// <summary>
    /// Gets the command and arguments (argv only, no shell).
    /// </summary>
    public required IReadOnlyList<string> Argv { get; init; }

    /// <summary>
    /// Gets the working directory.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets the environment variables.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>
    /// Gets the timeout.
    /// </summary>
    public required TimeSpan Timeout { get; init; }
}
