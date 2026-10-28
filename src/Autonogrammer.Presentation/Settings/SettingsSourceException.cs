namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The expected failure of a settings source: the values could not be read, or a change could not be written. The page turns it
/// into its error state or an inline save error; any other exception is a defect and escapes.
/// </summary>
public sealed class SettingsSourceException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public SettingsSourceException()
    {
    }

    /// <summary>Creates an exception with the reason shown to the user.</summary>
    /// <param name="message">The reason.</param>
    public SettingsSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the reason shown to the user and the failure behind it.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The failure behind it.</param>
    public SettingsSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
