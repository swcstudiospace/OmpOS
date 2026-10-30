namespace Autonogrammer.Worktrees;

/// <summary>
/// Raised when a run id is not 1 to 64 ASCII letters, digits or hyphens. Raised before any path or branch is touched.
/// </summary>
public sealed class InvalidRunIdException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance without a message or argument name.
    /// </summary>
    public InvalidRunIdException()
        : base()
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance for the rejected argument.
    /// </summary>
    /// <param name="paramName">The name of the rejected parameter.</param>
    /// <param name="runId">The rejected run id; may be null.</param>
    public InvalidRunIdException(string paramName, string? runId)
        : base("A run id must be 1 to 64 ASCII letters, digits or hyphens.", paramName)
    {
        RunId = runId;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidRunIdException(string message)
        : base(message)
    {
        RunId = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidRunIdException(string message, Exception innerException)
        : base(message, innerException)
    {
        RunId = null;
    }

    /// <summary>
    /// Gets the rejected run id, which may be null.
    /// </summary>
    public string? RunId { get; }
}
