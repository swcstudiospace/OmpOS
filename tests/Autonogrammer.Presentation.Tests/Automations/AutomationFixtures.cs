using Autonogrammer.Presentation.Automations;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>Builders for the drafts, entries and runs the hosted automations tests use.</summary>
internal static class AutomationFixtures
{
    /// <summary>The start of the runs the fixtures build.</summary>
    internal static readonly DateTimeOffset StartedAt = new(2026, 10, 28, 9, 0, 5, TimeSpan.Zero);

    /// <summary>Builds a valid cloud automation with a weekday schedule.</summary>
    /// <returns>The draft; change it with a <c>with</c> expression.</returns>
    internal static AutomationDraft Draft() => new(
        Name: "Nightly triage",
        Prompt: "Triage new issues and label them.",
        Enabled: true,
        TimeZone: "UTC",
        Triggers: [new AutomationTriggerDraft("cron", "0 9 * * 1-5", string.Empty, [], [], [])],
        TargetKind: "cloud",
        Repositories: [new AutomationRepositoryDraft("https://github.com/owner/repo", string.Empty, string.Empty)],
        SecretRefs: [],
        EnvironmentId: string.Empty,
        Model: string.Empty,
        Mode: string.Empty,
        AllowedTools: [AutomationEditorModel.PullRequestCreateTool],
        MaxConcurrentRuns: 1,
        MaxRunsPerHour: 12,
        MaxRuntimeMinutes: 30);

    /// <summary>Builds a webhook trigger.</summary>
    /// <param name="flavour">The webhook type.</param>
    /// <param name="events">The GitHub events.</param>
    /// <returns>The trigger.</returns>
    internal static AutomationTriggerDraft Webhook(string flavour = "generic", params string[] events) =>
        new("webhook", string.Empty, flavour, events, [], []);

    /// <summary>Builds an automation entry.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="draft">The settings; the default is <see cref="Draft"/>.</param>
    /// <param name="nextRunAt">The next scheduled run, or null.</param>
    /// <param name="lastRun">The latest run, or null.</param>
    /// <param name="webhookPath">The webhook path, or an empty string.</param>
    /// <param name="hasSecret">Whether a webhook secret is set.</param>
    /// <returns>The entry.</returns>
    internal static HostedAutomationEntry Entry(
        string id = "auto-1",
        AutomationDraft? draft = null,
        DateTimeOffset? nextRunAt = null,
        HostedRunEntry? lastRun = null,
        string webhookPath = "",
        bool hasSecret = false) =>
        new(id, draft ?? Draft(), nextRunAt, lastRun, webhookPath, hasSecret);

    /// <summary>Builds a run.</summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="status">The status.</param>
    /// <param name="trigger">The trigger: <c>cron</c>, <c>webhook</c> or <c>manual</c>.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="error">The error.</param>
    /// <param name="duration">The duration, or null.</param>
    /// <param name="externalId">The agent identifier.</param>
    /// <returns>The run.</returns>
    internal static HostedRunEntry Run(
        string runId,
        HostedRunStatus status,
        string trigger = "cron",
        string summary = "",
        string error = "",
        TimeSpan? duration = null,
        string externalId = "") =>
        new(runId, trigger, summary, status, StartedAt, duration, error, externalId);
}
