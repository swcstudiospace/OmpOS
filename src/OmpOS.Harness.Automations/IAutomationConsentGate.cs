namespace OmpOS.Harness.Automations;

/// <summary>
/// Answers whether the user has consented to the plugin hook that gates an automation. An automation's runs are refused
/// until its hook is consented to.
/// </summary>
public interface IAutomationConsentGate
{
    /// <summary>
    /// Returns true when consent is recorded for the hook's exact command.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <exception cref="ArgumentException">The hook is unknown. The engine refuses the run.</exception>
    bool IsConsented(string hookId);
}
