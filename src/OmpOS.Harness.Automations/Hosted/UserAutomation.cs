namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// An automation a user defined: a prompt, the triggers that start it and the target it runs on. It holds no secret value: the
/// webhook signing secret lives in an <see cref="IAutomationSecretStore"/> under <see cref="AutomationSecrets.WebhookReference"/>, and a
/// cloud target names its secrets. Instances are immutable; a change is a <c>with</c> copy saved to the store.
/// </summary>
public sealed record UserAutomation
{
    /// <summary>Gets the identifier. It is unguessable because it is part of the webhook URL.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the subject that owns the automation.</summary>
    public required string Owner { get; init; }

    /// <summary>Gets the display name, 1 to 100 characters.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the instructions each run starts with.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets whether triggers start runs. A disabled automation can still be run by hand.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the triggers, 1 to 8. The automation runs when any of them fires.</summary>
    public IReadOnlyList<AutomationTriggerSpec> Triggers { get; init; } = [];

    /// <summary>Gets where the runs execute.</summary>
    public AutomationTargetSpec Target { get; init; } = new();

    /// <summary>
    /// Gets the names of the tools the run may use beyond reading and editing (see <see cref="AutomationTools"/>). The list is passed to the
    /// target, which enforces it; this library only carries and validates the names.
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>Gets how many runs may be active at once, 1 to 4. Default 1.</summary>
    public int MaxConcurrentRuns { get; init; } = AutomationLimits.DefaultMaxConcurrentRuns;

    /// <summary>Gets how many runs may start in any rolling hour, 1 to 60.</summary>
    public int MaxRunsPerHour { get; init; } = AutomationLimits.DefaultMaxRunsPerHour;

    /// <summary>Gets the time zone the cron triggers are evaluated in: an IANA identifier, or <c>UTC</c>.</summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>Gets the longest a run may last, in minutes, 1 to 360. Applies to targets that run to completion.</summary>
    public int MaxRuntimeMinutes { get; init; } = AutomationLimits.DefaultMaxRuntimeMinutes;

    /// <summary>Gets when the automation was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the automation was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets whether the automation has a webhook trigger.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasWebhook => Triggers.Any(static trigger => trigger.Kind == AutomationTriggerKind.Webhook);
}
