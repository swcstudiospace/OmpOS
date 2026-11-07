using System.Globalization;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Scheduling;

/// <summary>
/// Session-target fields for the Automations settings page. The scheduler runs only while the desktop app is open.
/// The web list shows next run, last run and missed runs and does not start a task.
/// </summary>
public sealed class SessionTaskModel : ObservableModel
{
    /// <summary>Shown on the web. Scheduled tasks stay on the desktop.</summary>
    public const string DesktopOnlySentence = "Scheduled tasks run on the desktop while the app is open. This list shows the next run, the last run and missed runs.";

    /// <summary>Gets or sets the surface name, Code or Cowork.</summary>
    public string Surface { get; set; } = "Cowork";

    /// <summary>Gets or sets the project identifier, or null.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets the trusted folders on this device. They are not synced.</summary>
    public IReadOnlyList<string> TrustedFolders { get; set; } = [];

    /// <summary>Gets or sets the approval mode name: Manual, Auto or Skip.</summary>
    public string ApprovalMode { get; set; } = "Manual";

    /// <summary>Gets or sets the schedule preset name.</summary>
    public string SchedulePreset { get; set; } = "Daily";

    /// <summary>Gets or sets the missed-run policy name: RunOnce or Skip.</summary>
    public string MissedRunPolicy { get; set; } = "RunOnce";

    /// <summary>Gets or sets the next run, or null.</summary>
    public DateTimeOffset? NextRun { get; set; }

    /// <summary>Gets or sets the last run, or null.</summary>
    public DateTimeOffset? LastRun { get; set; }

    /// <summary>Gets or sets how many runs were missed.</summary>
    public int MissedRuns { get; set; }

    /// <summary>Gets the degraded sentence for a schedule state, or null.</summary>
    public string? Sentence { get; private set; }

    /// <summary>Gets the next-run wording.</summary>
    public string NextRunText => NextRun is null ? "Next run: none." : "Next run: " + NextRun.Value.ToString("u", CultureInfo.InvariantCulture);

    /// <summary>Gets the last-run wording.</summary>
    public string LastRunText => LastRun is null ? "Last run: none." : "Last run: " + LastRun.Value.ToString("u", CultureInfo.InvariantCulture);

    /// <summary>Gets the missed-runs wording.</summary>
    public string MissedRunsText => "Missed runs: " + MissedRuns.ToString(CultureInfo.InvariantCulture);

    /// <summary>Applies a D-29 schedule sentence.</summary>
    /// <param name="state">The state name.</param>
    public void ApplyState(string state)
    {
        Sentence = CoworkDegradedSentences.For(state);
        NotifyPropertyChanged(nameof(Sentence));
    }
}
