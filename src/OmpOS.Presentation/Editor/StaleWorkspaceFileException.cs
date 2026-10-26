namespace OmpOS.Presentation.Editor;

/// <summary>
/// Raised when a workspace file is written against content it no longer holds: the file on disk changed after the text the
/// write was based on was read. Nothing is written.
/// </summary>
public sealed class StaleWorkspaceFileException : IOException
{
    /// <summary>Initializes a new instance without a message.</summary>
    public StaleWorkspaceFileException()
        : base()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public StaleWorkspaceFileException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and an inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StaleWorkspaceFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
