using System.Globalization;

namespace Autonogrammer.Presentation.Automations;

/// <summary>One run in the history of an automation, as a head shows it. The row is immutable.</summary>
public sealed class HostedRunRowModel
{
    /// <summary>Creates the row for one run.</summary>
    /// <param name="entry">The run as the manager reports it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public HostedRunRowModel(HostedRunEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        RunId = entry.RunId;
        Status = entry.Status;
        StatusText = AutomationsCopy.StatusTitle(entry.Status);
        TriggerText = AutomationsCopy.TriggerSentence(entry.Trigger);
        WhenText = AutomationsCopy.FormatSecond(entry.StartedAt);
        DurationText = DescribeDuration(entry.Status, entry.Duration);
        SummaryText = entry.Summary.Length == 0 ? AutomationsCopy.NoSummary : entry.Summary;
        ErrorText = entry.Error;
        AgentText = entry.ExternalId.Length == 0 ? string.Empty : "Agent: " + entry.ExternalId;

        var duration = DurationText.Length == 0 ? string.Empty : ", " + DurationText;
        AccessibleName = $"{StatusText}, {WhenText}{duration}, {TriggerText}";
    }

    /// <summary>Gets the run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets how the run stands or ended.</summary>
    public HostedRunStatus Status { get; }

    /// <summary>Gets the status as a capitalised word, for example <c>Succeeded</c>.</summary>
    public string StatusText { get; }

    /// <summary>Gets what started the run, as a sentence.</summary>
    public string TriggerText { get; }

    /// <summary>Gets when the run started, in UTC.</summary>
    public string WhenText { get; }

    /// <summary>Gets how long the run took, <c>running</c> while it runs, or an empty string when the duration is not known.</summary>
    public string DurationText { get; }

    /// <summary>Gets what the run did, or a statement that it gave no summary.</summary>
    public string SummaryText { get; }

    /// <summary>Gets why the run failed, or an empty string when it did not.</summary>
    public string ErrorText { get; }

    /// <summary>Gets the identifier of the agent the run started, labelled, or an empty string when it started none.</summary>
    public string AgentText { get; }

    /// <summary>Gets the accessible name of the row.</summary>
    public string AccessibleName { get; }

    private static string DescribeDuration(HostedRunStatus status, TimeSpan? duration)
    {
        if (status == HostedRunStatus.Running)
        {
            return AutomationsCopy.DurationRunning;
        }

        if (duration is not { } taken)
        {
            return string.Empty;
        }

        var seconds = (long)Math.Max(0, Math.Floor(taken.TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var rest = seconds % 60;

        var parts = new List<string>();
        if (hours > 0)
        {
            parts.Add(hours.ToString(CultureInfo.InvariantCulture) + " h");
        }

        if (minutes > 0)
        {
            parts.Add(minutes.ToString(CultureInfo.InvariantCulture) + " min");
        }

        if (rest > 0 || parts.Count == 0)
        {
            parts.Add(rest.ToString(CultureInfo.InvariantCulture) + " s");
        }

        return "took " + string.Join(' ', parts);
    }
}
