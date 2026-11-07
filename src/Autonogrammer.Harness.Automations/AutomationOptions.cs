namespace Autonogrammer.Harness.Automations;

/// <summary>
/// Options for the automation engine. The unattended wait is not named like the conversation tool-host wait or the dispatch
/// command wait: those are different clocks.
/// </summary>
public sealed class AutomationOptions
{
    /// <summary>Gets how long an unattended run waits for approval before it is cancelled. Default 30 minutes. Nothing approves itself.</summary>
    public TimeSpan UnattendedApprovalTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets how old a missed session occurrence may be and still fold into one late run. Default 24 hours.</summary>
    public TimeSpan CatchUpWindow { get; init; } = TimeSpan.FromHours(24);
}
