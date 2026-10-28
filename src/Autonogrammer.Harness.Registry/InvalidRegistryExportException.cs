namespace Autonogrammer.Harness.Registry;

/// <summary>
/// Raised when a writer is opened over an export that is malformed or fails its checksum.
/// </summary>
public sealed class InvalidRegistryExportException : Exception
{
    /// <summary>
    /// Initializes a new instance without a path or reason.
    /// </summary>
    public InvalidRegistryExportException()
        : base("The registry export was refused.")
    {
        Path = null;
        Rejection = null;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidRegistryExportException(string message)
        : base(message)
    {
        Path = null;
        Rejection = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidRegistryExportException(string message, Exception innerException)
        : base(message, innerException)
    {
        Path = null;
        Rejection = null;
    }

    /// <summary>
    /// Initializes a new instance for an export that was refused.
    /// </summary>
    /// <param name="path">The export file.</param>
    /// <param name="rejection">Why the export was refused.</param>
    public InvalidRegistryExportException(string path, RegistryRejectionKind rejection)
        : base("The registry export was refused.")
    {
        Path = path;
        Rejection = rejection;
    }

    /// <summary>
    /// The export file that was refused, or null when the exception was built without one.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Why the export was refused, or null when the exception was built without a reason.
    /// </summary>
    public RegistryRejectionKind? Rejection { get; }
}
