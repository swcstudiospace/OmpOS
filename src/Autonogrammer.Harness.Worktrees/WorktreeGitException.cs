namespace Autonogrammer.Worktrees;

/// <summary>
/// Raised when a git command cannot be started or exits with a failure status.
/// </summary>
public sealed class WorktreeGitException : Exception
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public WorktreeGitException()
        : base()
    {
        ExitCode = null;
        StandardError = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance for a git command that exited with a failure status.
    /// </summary>
    /// <param name="message">A description of the failed operation.</param>
    /// <param name="exitCode">The exit code git returned.</param>
    /// <param name="standardError">The standard error text git wrote.</param>
    public WorktreeGitException(string message, int exitCode, string standardError)
        : base(message)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>
    /// Initializes a new instance for a git command that could not be started.
    /// </summary>
    /// <param name="message">A description of the failed operation.</param>
    /// <param name="innerException">The exception raised while starting the process.</param>
    public WorktreeGitException(string message, Exception innerException)
        : base(message, innerException)
    {
        ExitCode = null;
        StandardError = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorktreeGitException(string message)
        : base(message)
    {
        ExitCode = null;
        StandardError = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="exitCode">The exit code git returned, if any.</param>
    public WorktreeGitException(string message, Exception innerException, int? exitCode)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        StandardError = string.Empty;
    }

    /// <summary>
    /// Gets the git exit code, or null when the process could not be started.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets the text git wrote to standard error.
    /// </summary>
    public string StandardError { get; }
}
