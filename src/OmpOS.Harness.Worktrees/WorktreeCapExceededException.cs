namespace OmpOS.Worktrees;

/// <summary>
/// Raised when creating the worktrees of a run set would take the number of run worktrees above the cap. Nothing was created.
/// </summary>
public sealed class WorktreeCapExceededException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public WorktreeCapExceededException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorktreeCapExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WorktreeCapExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance for a cap that would be exceeded.
    /// </summary>
    /// <param name="cap">The most worktrees allowed.</param>
    /// <param name="existing">The worktrees that already exist under the root.</param>
    /// <param name="requested">The worktrees the run set needs.</param>
    public WorktreeCapExceededException(int cap, int existing, int requested)
        : base($"The run needs {requested} worktrees but {existing} already exist and the cap is {cap}. Close finished runs first.")
    {
        Cap = cap;
        Existing = existing;
        Requested = requested;
    }

    /// <summary>Gets the most worktrees allowed, or 0 when the exception was built from a message alone.</summary>
    public int Cap { get; }

    /// <summary>Gets the worktrees that already existed.</summary>
    public int Existing { get; }

    /// <summary>Gets the worktrees the run set needed.</summary>
    public int Requested { get; }
}
