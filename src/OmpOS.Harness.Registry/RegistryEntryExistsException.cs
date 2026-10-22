namespace OmpOS.Harness.Registry;

/// <summary>
/// Raised when an add names an identifier that is already registered.
/// </summary>
public sealed class RegistryEntryExistsException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public RegistryEntryExistsException()
        : base()
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RegistryEntryExistsException(string message)
        : base(message)
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RegistryEntryExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance for an identifier that is already registered.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="id">The identifier that is already registered.</param>
    public RegistryEntryExistsException(string message, string id)
        : base(message)
    {
        Id = id;
    }

    /// <summary>
    /// The identifier that is already registered, or null when the exception was built without one.
    /// </summary>
    public string? Id { get; }
}
