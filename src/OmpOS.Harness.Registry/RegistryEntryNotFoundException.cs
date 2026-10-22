namespace OmpOS.Harness.Registry;

/// <summary>
/// Raised when a mutation names an identifier that is not registered.
/// </summary>
public sealed class RegistryEntryNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance without a message.
    /// </summary>
    public RegistryEntryNotFoundException()
        : base()
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RegistryEntryNotFoundException(string message)
        : base(message)
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RegistryEntryNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
        Id = null;
    }

    /// <summary>
    /// Initializes a new instance for an identifier that is not registered.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="id">The identifier that is not registered.</param>
    public RegistryEntryNotFoundException(string message, string id)
        : base(message)
    {
        Id = id;
    }

    /// <summary>
    /// The identifier that is not registered, or null when the exception was built without one.
    /// </summary>
    public string? Id { get; }
}
