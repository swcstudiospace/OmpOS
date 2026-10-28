namespace Autonogrammer.Worktrees;

/// <summary>
/// Raised when the roots of a run set are the same folder or one lies inside another. Two worktrees of overlapping folders would let
/// one root's agent edits appear inside the other.
/// </summary>
public sealed class NestedWorktreeRootException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public NestedWorktreeRootException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NestedWorktreeRootException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NestedWorktreeRootException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
