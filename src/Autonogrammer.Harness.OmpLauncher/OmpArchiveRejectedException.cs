namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// An archive entry was refused before anything was written. The message names the entry and the rule it broke.
/// </summary>
public sealed class OmpArchiveRejectedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public OmpArchiveRejectedException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was refused.</param>
    public OmpArchiveRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was refused.</param>
    /// <param name="innerException">The error that caused the refusal.</param>
    public OmpArchiveRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
