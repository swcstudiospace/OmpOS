using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// Raised when a store refuses a write because the content cap was exceeded. Nothing is truncated.
/// </summary>
public sealed class ArtifactQuotaException : Exception
{
    private readonly ArtifactFailure _failure = ArtifactFailure.QuotaExceeded;

    /// <summary>Initializes a new instance of the <see cref="ArtifactQuotaException"/> class.</summary>
    public ArtifactQuotaException()
        : base(ArtifactFailure.QuotaExceeded.Code)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArtifactQuotaException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ArtifactQuotaException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArtifactQuotaException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ArtifactQuotaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the typed quota failure.</summary>
    public ArtifactFailure Failure => _failure;
}
