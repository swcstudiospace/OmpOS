using System.Globalization;

namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// One automation in the list, as a head shows it: its trigger, enable toggle, latest outcome and, when a guard paused it, the guard
/// and how to resume. The row is immutable; the actions on it are methods of the <see cref="AutomationsViewModel"/> that owns the list.
/// </summary>
public sealed class AutomationRowModel
{
    /// <summary>Creates the row for one automation.</summary>
    /// <param name="entry">The automation as the source reports it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public AutomationRowModel(AutomationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Id = entry.Id;
        IsEnabled = entry.IsEnabled;
        IsPaused = entry.Pause is not null;
        LastOutcome = entry.LastRun?.Outcome;

        TriggerText = "Trigger: " + entry.Trigger;
        MaxRuntimeText = "Maximum runtime: " + entry.MaxRuntime.ToString("c", CultureInfo.InvariantCulture);
        EnabledText = IsEnabled ? "Enabled" : "Disabled";
        StatusText = IsPaused ? "Paused" : EnabledText;
        LastOutcomeText = DescribeLastRun(entry.LastRun);
        PauseText = entry.Pause is null ? string.Empty : $"Paused by guard {entry.Pause.Guard}: {entry.Pause.Reason}";
        ResumeText = entry.Pause is null ? string.Empty : "To resume: " + entry.Pause.HowToResume;

        ToggleText = IsEnabled ? "Turn off" : "Turn on";
        ToggleName = (IsEnabled ? "Disable automation " : "Enable automation ") + Id;
        ResumeName = "Resume automation " + Id;
    }

    /// <summary>Gets the automation identifier.</summary>
    public string Id { get; }

    /// <summary>Gets whether the automation is enabled. The enable toggle shows this state.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets whether a guard paused the automation. Such a row carries the paused banner and refuses every run until resumed.</summary>
    public bool IsPaused { get; }

    /// <summary>Gets how the latest run attempt ended, or null when the automation has not been attempted.</summary>
    public AutomationOutcome? LastOutcome { get; }

    /// <summary>Gets the trigger, labelled.</summary>
    public string TriggerText { get; }

    /// <summary>Gets the maximum runtime, labelled.</summary>
    public string MaxRuntimeText { get; }

    /// <summary>Gets the enabled state as text: <c>Enabled</c> or <c>Disabled</c>.</summary>
    public string EnabledText { get; }

    /// <summary>Gets the status as text: <c>Paused</c> while a guard holds the automation, otherwise the enabled state.</summary>
    public string StatusText { get; }

    /// <summary>Gets the latest run attempt as text, or a statement that the automation has not run.</summary>
    public string LastOutcomeText { get; }

    /// <summary>Gets the banner that names the guard and why it paused the automation, or an empty string when the automation is not paused.</summary>
    public string PauseText { get; }

    /// <summary>Gets what an operator does to resume the automation, or an empty string when the automation is not paused.</summary>
    public string ResumeText { get; }

    /// <summary>Gets the text of the enable toggle: the change it makes.</summary>
    public string ToggleText { get; }

    /// <summary>Gets the accessible name of the enable toggle.</summary>
    public string ToggleName { get; }

    /// <summary>Gets the accessible name of the Resume button.</summary>
    public string ResumeName { get; }

    private static string DescribeLastRun(AutomationLastRun? lastRun)
    {
        if (lastRun is null)
        {
            return "Last run: never";
        }

        var outcome = lastRun.Outcome switch
        {
            AutomationOutcome.Succeeded => "succeeded",
            AutomationOutcome.Failed => "failed",
            AutomationOutcome.TimedOut => "timed out",
            AutomationOutcome.Cancelled => "cancelled",
            AutomationOutcome.Blocked => "blocked",
            AutomationOutcome.Refused => "refused",
            _ => "unknown",
        };

        var at = lastRun.RecordedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var text = $"Last run: {outcome} at {at} UTC";
        return lastRun.Reason.Length == 0 ? text : $"{text} ({lastRun.Reason})";
    }
}
