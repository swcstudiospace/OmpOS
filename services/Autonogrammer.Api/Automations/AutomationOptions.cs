namespace Autonogrammer.Api.Automations;

/// <summary>
/// The <c>Automations</c> configuration section. Automations are off by default: with <see cref="Enabled"/> false every automation route answers 404,
/// no schedule runs and the settings answer <c>GET /automations</c> keeps saying that none is configured. The API process that has this
/// on must be the only one that schedules (see <c>docs/reference/automations.md</c>): two schedulers on one store would fire a cron
/// occurrence twice.
/// </summary>
internal sealed class AutomationOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Automations";

    /// <summary>The default number of webhook deliveries allowed per minute across all senders.</summary>
    public const int DefaultWebhookRequestsPerMinute = 120;

    /// <summary>The default number of automation management calls allowed per minute across all callers.</summary>
    public const int DefaultRequestsPerMinute = 120;

    /// <summary>Gets or sets a value indicating whether the automation routes, the scheduler and the webhook receiver are on. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the absolute path of the automation store file. Empty keeps the automations in memory, which a restart loses.</summary>
    public string StorePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path of the webhook secret file (owner-only, not encrypted at rest). Empty keeps the secrets in memory: a restart
    /// loses them, so every webhook must then be given a rotated secret. A host that needs a vault registers its own <c>IAutomationSecretStore</c>.
    /// </summary>
    public string SecretStorePath { get; set; } = string.Empty;

    /// <summary>Gets or sets whether this process starts cron runs. Default true. Set false on every replica but one.</summary>
    public bool SchedulerEnabled { get; set; } = true;

    /// <summary>Gets or sets how many webhook deliveries are accepted per minute across all senders, 1 to 100000.</summary>
    public int WebhookRequestsPerMinute { get; set; } = DefaultWebhookRequestsPerMinute;

    /// <summary>Gets or sets how many automation management calls are accepted per minute across all callers, 1 to 100000.</summary>
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;

    /// <summary>Gets or sets how far a generic delivery's timestamp may be from the server clock, in seconds, 30 to 3600. Default 300.</summary>
    public int WebhookReplayWindowSeconds { get; set; } = 300;
}
