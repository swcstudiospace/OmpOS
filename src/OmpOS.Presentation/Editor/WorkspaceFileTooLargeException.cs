namespace OmpOS.Presentation.Editor;

/// <summary>Raised when a workspace file holds more bytes than the caller allowed to read. Nothing of the file is returned.</summary>
public sealed class WorkspaceFileTooLargeException : IOException
{
    /// <summary>Initializes a new instance without a message.</summary>
    public WorkspaceFileTooLargeException()
        : base()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public WorkspaceFileTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and an inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WorkspaceFileTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
