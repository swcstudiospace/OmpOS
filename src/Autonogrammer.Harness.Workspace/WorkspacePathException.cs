namespace Autonogrammer.Workspace;

/// <summary>
/// Raised when a workspace-relative path is rejected because it would leave the workspace root.
/// </summary>
public sealed class WorkspacePathException : Exception
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public WorkspacePathException()
        : base()
    {
        RequestedPath = string.Empty;
        Reason = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorkspacePathException(string message)
        : base(message)
    {
        RequestedPath = string.Empty;
        Reason = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WorkspacePathException(string message, Exception innerException)
        : base(message, innerException)
    {
        RequestedPath = string.Empty;
        Reason = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance for a rejected workspace-relative path.
    /// </summary>
    /// <param name="requestedPath">The path as the caller supplied it.</param>
    /// <param name="reason">Why the path was rejected, without a trailing period.</param>
    public WorkspacePathException(string requestedPath, string reason)
        : base($"Workspace path '{requestedPath}' is rejected: {reason}.")
    {
        RequestedPath = requestedPath;
        Reason = reason;
    }

    /// <summary>
    /// Gets the path as the caller supplied it.
    /// </summary>
    public string RequestedPath { get; }

    /// <summary>
    /// Gets why the path was rejected.
    /// </summary>
    public string Reason { get; }
}
