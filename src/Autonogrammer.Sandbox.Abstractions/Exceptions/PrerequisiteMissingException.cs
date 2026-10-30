namespace Autonogrammer.Sandbox.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a prerequisite is missing.
/// </summary>
public sealed class PrerequisiteMissingException : SandboxException
{
    /// <inheritdoc />
    public override string ErrorCode => "SBOX_PREREQUISITE_MISSING";

    /// <summary>
    /// Gets the name of the missing prerequisite.
    /// </summary>
    public string PrerequisiteName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteMissingException"/> class for a missing prerequisite.
    /// </summary>
    /// <param name="prerequisiteName">The name of the missing prerequisite.</param>
    /// <param name="message">The error message.</param>
    public PrerequisiteMissingException(string prerequisiteName, string message)
        : base(message)
    {
        PrerequisiteName = prerequisiteName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteMissingException"/> class for a missing prerequisite with an inner exception.
    /// </summary>
    /// <param name="prerequisiteName">The name of the missing prerequisite.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrerequisiteMissingException(string prerequisiteName, string message, Exception innerException)
        : base(message, innerException)
    {
        PrerequisiteName = prerequisiteName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteMissingException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PrerequisiteMissingException(string message)
        : base(message)
    {
        PrerequisiteName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteMissingException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PrerequisiteMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
        PrerequisiteName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteMissingException"/> class with a default message.
    /// </summary>
    public PrerequisiteMissingException()
        : base()
    {
        PrerequisiteName = string.Empty;
    }
}
