namespace Autonogrammer.Worktrees;

/// <summary>
/// Raised when no worktree exists under the operator root for a run id.
/// </summary>
public sealed class RunNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public RunNotFoundException()
        : base()
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance for a run id that has no worktree.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="runId">The run id that has no worktree.</param>
    public RunNotFoundException(string message, string runId)
        : base(message)
    {
        RunId = runId;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RunNotFoundException(string message)
        : base(message)
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RunNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
        RunId = null;
    }

    /// <summary>
    /// Gets the run id that has no worktree, or null when the exception was built from a message alone.
    /// </summary>
    public string? RunId { get; }
}
