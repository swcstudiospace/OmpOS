namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>
/// Raised when stored bytes do not match their hash or the parent link is broken. Callers must not render content.
/// </summary>
public sealed class ArtifactIntegrityException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ArtifactIntegrityException"/> class.</summary>
    public ArtifactIntegrityException()
        : base("Artifact integrity check failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArtifactIntegrityException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ArtifactIntegrityException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArtifactIntegrityException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ArtifactIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
