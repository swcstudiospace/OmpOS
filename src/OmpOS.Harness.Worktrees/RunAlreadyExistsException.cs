namespace OmpOS.Worktrees;

/// <summary>
/// Raised when a run with the same id already has a worktree path or branch, or is being created.
/// </summary>
public sealed class RunAlreadyExistsException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public RunAlreadyExistsException()
        : base()
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance for a run id that is already in use.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="runId">The run id that is already in use.</param>
    public RunAlreadyExistsException(string message, string runId)
        : base(message)
    {
        RunId = runId;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RunAlreadyExistsException(string message)
        : base(message)
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RunAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
        RunId = null;
    }

    /// <summary>
    /// Gets the run id that is already in use, or null when the exception was built from a message alone.
    /// </summary>
    public string? RunId { get; }
}
