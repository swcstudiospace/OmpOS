namespace Autonogrammer.Sandbox.Abstractions;

/// <summary>
/// Execution request.
/// </summary>
public sealed record ExecRequest
{
    /// <summary>Gets the command and arguments.</summary>
    public required IReadOnlyList<string> Argv { get; init; }

    /// <summary>Gets the working directory.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the environment variables.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the timeout.</summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>Gets whether to allocate a TTY.</summary>
    public required bool Tty { get; init; }
}
