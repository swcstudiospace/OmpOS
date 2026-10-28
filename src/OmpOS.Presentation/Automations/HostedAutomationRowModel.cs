using System.Globalization;

namespace OmpOS.Presentation.Automations;

/// <summary>
/// One hosted automation in the list, as a head shows it: its triggers, target, next run, latest result and the accessible names of its
/// actions. The row is immutable; the actions on it are methods of the <see cref="HostedAutomationsViewModel"/> that owns the list. No
/// text holds a webhook secret: an entry never carries one.
/// </summary>
public sealed class HostedAutomationRowModel
{
    private const string CronKind = "cron";
    private const string WebhookKind = "webhook";
    private const string GithubFlavour = "github";
    private const string CloudKind = "cloud";
    private const string LocalKind = "local";

    /// <summary>Creates the row for one automation.</summary>
    /// <param name="entry">The automation as the manager reports it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    /// <exception cref="ArgumentException">The entry's draft is null.</exception>
    public HostedAutomationRowModel(HostedAutomationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Draft is null)
        {
            throw new ArgumentException("The entry has no draft.", nameof(entry));
        }

        var draft = entry.Draft;
        Id = entry.Id;
        Name = draft.Name;
        IsEnabled = draft.Enabled;
        WebhookPath = entry.WebhookPath;

        EnabledText = IsEnabled ? "Enabled" : "Disabled";
        TriggerText = DescribeTriggers(draft);
        TargetText = DescribeTarget(draft);
        NextRunText = entry.NextRunAt is { } next
            ? "Next run: " + AutomationsCopy.FormatMinute(next)
            : AutomationsCopy.NextRunNotScheduled;
        LastResultText = DescribeLastRun(entry.LastRun);
        WebhookText = entry.WebhookPath.Length == 0 ? string.Empty : AutomationsCopy.WebhookPathLabel + ": " + entry.WebhookPath;

        ToggleText = IsEnabled ? AutomationsCopy.TurnOffLabel : AutomationsCopy.TurnOnLabel;
        ToggleName = AutomationsCopy.ToggleName(Name, IsEnabled);
        RunNowName = AutomationsCopy.RunNowName(Name);
        EditName = AutomationsCopy.EditName(Name);
        DeleteName = AutomationsCopy.DeleteName(Name);
        AccessibleName = $"{Name}, {EnabledText}, {TargetText}, {NextRunText}";
    }

    /// <summary>Gets the automation identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the automation is enabled. The enable toggle shows this state.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets the enabled state as text: <c>Enabled</c> or <c>Disabled</c>.</summary>
    public string EnabledText { get; }

    /// <summary>Gets the triggers as text: a schedule line and a webhook line, joined by a semicolon.</summary>
    public string TriggerText { get; }

    /// <summary>Gets where runs happen: a local session or a cloud agent with its first repository.</summary>
    public string TargetText { get; }

    /// <summary>Gets when the next scheduled run starts, in UTC, or a statement that none is scheduled.</summary>
    public string NextRunText { get; }

    /// <summary>Gets the latest run as text, or a statement that the automation has not run.</summary>
    public string LastResultText { get; }

    /// <summary>Gets the path the webhook receives on, or an empty string when the automation has no webhook trigger.</summary>
    public string WebhookPath { get; }

    /// <summary>Gets the webhook path as a labelled line, or an empty string when the automation has no webhook trigger.</summary>
    public string WebhookText { get; }

    /// <summary>Gets the text of the enable toggle: the change it makes.</summary>
    public string ToggleText { get; }

    /// <summary>Gets the accessible name of the enable toggle.</summary>
    public string ToggleName { get; }

    /// <summary>Gets the accessible name of the Run now button.</summary>
    public string RunNowName { get; }

    /// <summary>Gets the accessible name of the Edit button.</summary>
    public string EditName { get; }

    /// <summary>Gets the accessible name of the Delete button.</summary>
    public string DeleteName { get; }

    /// <summary>Gets the accessible name of the row in the list.</summary>
    public string AccessibleName { get; }

    private static string DescribeTriggers(AutomationDraft draft)
    {
        var parts = new List<string>();
        foreach (var trigger in draft.Triggers)
        {
            if (string.Equals(trigger.Kind, CronKind, StringComparison.Ordinal))
            {
                parts.Add($"Schedule: {trigger.Cron} ({draft.TimeZone})");
            }
            else if (string.Equals(trigger.Kind, WebhookKind, StringComparison.Ordinal))
            {
                parts.Add(DescribeWebhook(trigger));
            }
            else
            {
                parts.Add(trigger.Kind);
            }
        }

        return parts.Count == 0 ? AutomationsCopy.NoTriggers : string.Join("; ", parts);
    }

    private static string DescribeWebhook(AutomationTriggerDraft trigger)
    {
        if (!string.Equals(trigger.Flavour, GithubFlavour, StringComparison.Ordinal))
        {
            return "Webhook";
        }

        return trigger.Events.Count == 0
            ? "Webhook: GitHub"
            : "Webhook: GitHub " + string.Join(", ", trigger.Events.Select(static name => name.Replace('_', ' ')));
    }

    private static string DescribeTarget(AutomationDraft draft)
    {
        if (string.Equals(draft.TargetKind, LocalKind, StringComparison.Ordinal))
        {
            return AutomationsCopy.LocalTarget;
        }

        if (!string.Equals(draft.TargetKind, CloudKind, StringComparison.Ordinal))
        {
            return draft.TargetKind;
        }

        if (draft.Repositories.Count == 0)
        {
            return AutomationsCopy.CloudTargetNoRepository;
        }

        var text = AutomationsCopy.CloudTargetNoRepository + ": " + RepositoryPath(draft.Repositories[0].Address);
        return draft.Repositories.Count == 1
            ? text
            : text + " (+" + (draft.Repositories.Count - 1).ToString(CultureInfo.InvariantCulture) + " more)";
    }

    private static string RepositoryPath(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (path.EndsWith(".git", StringComparison.Ordinal))
            {
                path = path[..^4];
            }

            if (path.Length > 0)
            {
                return path;
            }
        }

        return url;
    }

    private static string DescribeLastRun(HostedRunEntry? lastRun)
    {
        if (lastRun is null)
        {
            return AutomationsCopy.LastRunNever;
        }

        var at = AutomationsCopy.FormatSecond(lastRun.StartedAt);
        if (lastRun.Status == HostedRunStatus.Running)
        {
            return "Last run: running since " + at;
        }

        var text = $"Last run: {AutomationsCopy.StatusWord(lastRun.Status)} at {at}";
        var reason = lastRun.Error.Length > 0 ? lastRun.Error : lastRun.Summary;
        return reason.Length == 0 ? text : $"{text} ({reason})";
    }
}
