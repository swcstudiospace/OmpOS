using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>Builders for the hosted automation model used across the tests.</summary>
internal static class TestAutomations
{
    /// <summary>The owner of every automation built here unless a test says otherwise.</summary>
    public const string Alice = "alice";

    /// <summary>Builds a valid cloud automation with the given triggers.</summary>
    /// <param name="triggers">The triggers; a single cron trigger when none are given.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="enabled">Whether triggers start runs.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="timeZone">The time zone identifier.</param>
    /// <returns>The automation.</returns>
    public static UserAutomation Make(
        AutomationTriggerSpec[]? triggers = null,
        string id = "aut_0001",
        bool enabled = true,
        string owner = Alice,
        string timeZone = "UTC") =>
        new()
        {
            Id = id,
            Owner = owner,
            Name = "Nightly review",
            Prompt = "Review the changes of the last day.",
            Enabled = enabled,
            Triggers = triggers ?? [Cron("0 9 * * *")],
            Target = new AutomationTargetSpec { Repositories = [new AutomationRepository("https://github.com/acme/widgets")] },
            AllowedTools = AutomationTools.Default,
            TimeZone = timeZone,
            CreatedAt = Kit.Start,
            UpdatedAt = Kit.Start,
        };

    /// <summary>Builds a cron trigger.</summary>
    /// <param name="expression">The cron expression.</param>
    /// <returns>The trigger.</returns>
    public static AutomationTriggerSpec Cron(string expression) => new() { Kind = AutomationTriggerKind.Cron, Cron = expression };

    /// <summary>Builds a generic webhook trigger.</summary>
    /// <returns>The trigger.</returns>
    public static AutomationTriggerSpec GenericHook() => new() { Kind = AutomationTriggerKind.Webhook, Flavour = WebhookFlavour.Generic };

    /// <summary>Builds a GitHub webhook trigger with filters.</summary>
    /// <param name="events">The accepted events; empty accepts both.</param>
    /// <param name="actions">The accepted pull request actions; empty accepts all.</param>
    /// <param name="branches">The accepted branch globs; empty accepts all.</param>
    /// <returns>The trigger.</returns>
    public static AutomationTriggerSpec GitHubHook(string[]? events = null, string[]? actions = null, string[]? branches = null) =>
        new()
        {
            Kind = AutomationTriggerKind.Webhook,
            Flavour = WebhookFlavour.GitHub,
            Events = events ?? [],
            Actions = actions ?? [],
            Branches = branches ?? [],
        };

    /// <summary>Builds a draft that is valid for <see cref="AutomationService"/>.</summary>
    /// <param name="triggers">The triggers; a single cron trigger when none are given.</param>
    /// <returns>The draft.</returns>
    public static AutomationDraft Draft(params AutomationTriggerSpec[] triggers) =>
        new()
        {
            Name = "Nightly review",
            Prompt = "Review the changes of the last day.",
            Enabled = true,
            Triggers = triggers.Length == 0 ? [Cron("0 9 * * *")] : triggers,
            Target = new AutomationTargetSpec { Repositories = [new AutomationRepository("https://github.com/acme/widgets")] },
        };
}
