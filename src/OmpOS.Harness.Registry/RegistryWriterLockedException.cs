namespace OmpOS.Harness.Registry;

/// <summary>
/// Raised when a writer is opened over an export that another writer already holds. A registry has exactly one writer.
/// </summary>
public sealed class RegistryWriterLockedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public RegistryWriterLockedException()
        : base()
    {
        ExportPath = null;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RegistryWriterLockedException(string message)
        : base(message)
    {
        ExportPath = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RegistryWriterLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
        ExportPath = null;
    }

    /// <summary>
    /// Initializes a new instance for an export that another writer holds.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exportPath">The export file that is held.</param>
    /// <param name="innerException">The sharing failure reported by the file system.</param>
    public RegistryWriterLockedException(string message, string exportPath, Exception innerException)
        : base(message, innerException)
    {
        ExportPath = exportPath;
    }

    /// <summary>
    /// The export file that is held by another writer, or null when the exception was built without one.
    /// </summary>
    public string? ExportPath { get; }
}
