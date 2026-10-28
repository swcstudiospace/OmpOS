namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// Thrown by <see cref="IAutomationsManager.ListAsync"/> when the service has automations switched off. The
/// <see cref="HostedAutomationsViewModel"/> turns it into the unavailable state, which explains how to switch automations on.
/// </summary>
public sealed class AutomationsUnavailableException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public AutomationsUnavailableException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">The reason.</param>
    public AutomationsUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and the failure behind it.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The failure behind it.</param>
    public AutomationsUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
