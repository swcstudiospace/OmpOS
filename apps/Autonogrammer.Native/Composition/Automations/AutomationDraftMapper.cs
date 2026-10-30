using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Presentation.Automations;
using HostedDraft = Autonogrammer.Harness.Automations.Hosted.AutomationDraft;
using ViewDraft = Autonogrammer.Presentation.Automations.AutomationDraft;
using ViewTrigger = Autonogrammer.Presentation.Automations.AutomationTriggerDraft;

namespace Autonogrammer.Native.Composition.Automations;

/// <summary>Maps between the drafts and entries of the presentation layer (strings, so a view binds to them) and the automation harness's records.</summary>
internal static class AutomationDraftMapper
{
    internal const string CronKind = "cron";
    internal const string WebhookKind = "webhook";
    internal const string LocalKind = "local";
    internal const string CloudKind = "cloud";

    /// <summary>Maps a draft of the form to the harness's draft.</summary>
    /// <param name="draft">The form's draft. A webhook trigger is not mapped: the caller refuses it first.</param>
    /// <returns>The harness draft.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public static HostedDraft ToHosted(ViewDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new HostedDraft
        {
            Name = draft.Name,
            Prompt = draft.Prompt,
            Enabled = draft.Enabled,
            TimeZone = draft.TimeZone,
            Triggers = [.. draft.Triggers.Where(static trigger => string.Equals(trigger.Kind, CronKind, StringComparison.Ordinal)).Select(static trigger => new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = trigger.Cron })],
            Target = new AutomationTargetSpec
            {
                Kind = string.Equals(draft.TargetKind, LocalKind, StringComparison.Ordinal) ? AutomationTargetKind.Local : AutomationTargetKind.Cloud,
                Repositories = [.. draft.Repositories.Select(static repository => new AutomationRepository(repository.Address, NullIfEmpty(repository.Branch), NullIfEmpty(repository.BaseBranch)))],
                SecretRefs = [.. draft.SecretRefs],
                EnvironmentId = NullIfEmpty(draft.EnvironmentId),
                Model = NullIfEmpty(draft.Model),
                Mode = NullIfEmpty(draft.Mode),
            },
            AllowedTools = [.. draft.AllowedTools],
            MaxConcurrentRuns = draft.MaxConcurrentRuns,
            MaxRunsPerHour = draft.MaxRunsPerHour,
            MaxRuntimeMinutes = draft.MaxRuntimeMinutes,
        };
    }

    /// <summary>Maps a stored automation to the draft the form edits.</summary>
    /// <param name="automation">The automation.</param>
    /// <returns>The draft.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="automation"/> is null.</exception>
    public static ViewDraft ToView(UserAutomation automation)
    {
        ArgumentNullException.ThrowIfNull(automation);
        var target = automation.Target;
        return new ViewDraft(
            automation.Name,
            automation.Prompt,
            automation.Enabled,
            automation.TimeZone,
            [.. automation.Triggers.Select(ToView)],
            target.Kind == AutomationTargetKind.Local ? LocalKind : CloudKind,
            [.. target.Repositories.Select(static repository => new AutomationRepositoryDraft(repository.Address, repository.Branch ?? string.Empty, repository.BaseBranch ?? string.Empty))],
            [.. target.SecretRefs],
            target.EnvironmentId ?? string.Empty,
            target.Model ?? string.Empty,
            target.Mode ?? string.Empty,
            [.. automation.AllowedTools],
            automation.MaxConcurrentRuns,
            automation.MaxRunsPerHour,
            automation.MaxRuntimeMinutes);
    }

    /// <summary>Maps a run of the history to the row the page lists.</summary>
    /// <param name="entry">The run.</param>
    /// <returns>The row's record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static HostedRunEntry ToView(AutomationRunEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new HostedRunEntry(
            entry.RunId,
            entry.Source switch
            {
                AutomationRunSource.Cron => CronKind,
                AutomationRunSource.Webhook => WebhookKind,
                _ => "manual",
            },
            entry.Summary,
            entry.Status switch
            {
                AutomationRunStatus.Running => HostedRunStatus.Running,
                AutomationRunStatus.Succeeded => HostedRunStatus.Succeeded,
                AutomationRunStatus.Launched => HostedRunStatus.Launched,
                AutomationRunStatus.TimedOut => HostedRunStatus.TimedOut,
                AutomationRunStatus.Cancelled => HostedRunStatus.Cancelled,
                AutomationRunStatus.Skipped => HostedRunStatus.Skipped,
                _ => HostedRunStatus.Failed,
            },
            entry.StartedAt,
            entry.Duration,
            entry.Error ?? string.Empty,
            entry.ExternalId ?? string.Empty);
    }

    private static ViewTrigger ToView(AutomationTriggerSpec trigger) =>
        trigger.Kind == AutomationTriggerKind.Cron
            ? new ViewTrigger(CronKind, trigger.Cron ?? string.Empty, string.Empty, [], [], [])
            : new ViewTrigger(WebhookKind, string.Empty, trigger.Flavour == WebhookFlavour.GitHub ? "github" : "generic", [.. trigger.Events], [.. trigger.Actions], [.. trigger.Branches]);

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
