namespace Autonogrammer.Presentation.Hooks;

/// <summary>
/// The expected failure of an <see cref="IHooksSource"/>: the registry could not be loaded, or a change could not be written.
/// A load turns it into the error state and a change into the action failure text. For a change, any other exception is reported as the unexpected-failure text,
/// except <see cref="OutOfMemoryException"/>, which escapes; a load exception other than this one escapes too.
/// </summary>
public sealed class HooksSourceException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public HooksSourceException()
    {
    }

    /// <summary>Creates an exception with the reason shown to the user.</summary>
    /// <param name="message">The reason.</param>
    public HooksSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the reason shown to the user and the failure behind it.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The failure behind it.</param>
    public HooksSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
