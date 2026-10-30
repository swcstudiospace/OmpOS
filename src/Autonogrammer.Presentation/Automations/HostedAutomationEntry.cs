namespace Autonogrammer.Presentation.Automations;

/// <summary>One hosted automation as an <see cref="IAutomationsManager"/> reports it. Whether it is enabled is <c>Draft.Enabled</c>.</summary>
/// <param name="Id">The automation identifier.</param>
/// <param name="Draft">The automation's settings.</param>
/// <param name="NextRunAt">When the next scheduled run starts, or null when none is scheduled.</param>
/// <param name="LastRun">The latest run, or null when the automation has not run.</param>
/// <param name="WebhookPath">The path the webhook receives on, or an empty string when the automation has no webhook trigger.</param>
/// <param name="HasWebhookSecret">Whether a webhook secret is set. The secret itself is never part of an entry.</param>
public sealed record HostedAutomationEntry(
    string Id,
    AutomationDraft Draft,
    DateTimeOffset? NextRunAt,
    HostedRunEntry? LastRun,
    string WebhookPath,
    bool HasWebhookSecret);
