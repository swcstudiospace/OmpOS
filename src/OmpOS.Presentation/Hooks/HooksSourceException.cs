namespace OmpOS.Presentation.Hooks;

/// <summary>
/// The expected failure of an <see cref="IHooksSource"/>: the registry could not be loaded, or a change could not be written.
/// <see cref="HooksViewModel"/> turns it into the error state or the action failure text; any other exception is a defect and escapes.
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
