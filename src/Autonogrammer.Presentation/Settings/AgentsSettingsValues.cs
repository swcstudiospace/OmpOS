namespace Autonogrammer.Presentation.Settings;

/// <summary>The saved values of the Agents settings page.</summary>
/// <param name="TerminalAutoRun">True when proposed terminal commands run without waiting for an approval.</param>
public sealed record AgentsSettingsValues(bool TerminalAutoRun)
{
    /// <summary>Gets the page defaults: every terminal command waits for an approval.</summary>
    public static AgentsSettingsValues Defaults { get; } = new(TerminalAutoRun: false);
}
