namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// The expected failure of an <see cref="IAutomationsSource"/>: the automations could not be loaded, or a change could not be written.
/// <see cref="AutomationsViewModel"/> turns it into the error state or the action failure text; any other exception is a defect and escapes.
/// </summary>
public sealed class AutomationsSourceException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public AutomationsSourceException()
    {
    }

    /// <summary>Creates an exception with the reason shown to the user.</summary>
    /// <param name="message">The reason.</param>
    public AutomationsSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the reason shown to the user and the failure behind it.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The failure behind it.</param>
    public AutomationsSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
