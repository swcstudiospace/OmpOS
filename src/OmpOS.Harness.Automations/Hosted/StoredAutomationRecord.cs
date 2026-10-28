namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// The stored state of one automation: its definition, its schedule cursor and its run history, newest first.
/// </summary>
internal sealed class StoredAutomationRecord
{
    /// <summary>Gets or sets the definition.</summary>
    public UserAutomation? Automation { get; set; }

    /// <summary>Gets or sets the instant up to which cron occurrences are handled.</summary>
    public DateTimeOffset? ScheduleCursor { get; set; }

    /// <summary>Gets or sets the run history, newest first.</summary>
    public List<AutomationRunEntry>? Runs { get; set; }
}
