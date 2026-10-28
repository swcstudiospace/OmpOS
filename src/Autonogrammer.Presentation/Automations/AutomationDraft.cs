namespace Autonogrammer.Presentation.Automations;

/// <summary>An automation as the editor writes it: a prompt, its triggers and where it runs.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Prompt">The prompt each run is given.</param>
/// <param name="Enabled">Whether the automation starts runs.</param>
/// <param name="TimeZone">The IANA time zone cron schedules are read in.</param>
/// <param name="Triggers">The triggers that start a run.</param>
/// <param name="TargetKind">Where runs happen: <c>local</c> (a local session) or <c>cloud</c> (a cloud agent).</param>
/// <param name="Repositories">The repositories of a cloud target; empty for a local target.</param>
/// <param name="SecretRefs">The names, never the values, of stored secrets a cloud agent may read.</param>
/// <param name="EnvironmentId">The cloud environment, or an empty string for the host's default.</param>
/// <param name="Model">The model, or an empty string for the host's default.</param>
/// <param name="Mode">The agent mode, or an empty string for the host's default.</param>
/// <param name="AllowedTools">The pull request actions a run may take.</param>
/// <param name="MaxConcurrentRuns">How many runs may be in progress at once.</param>
/// <param name="MaxRunsPerHour">How many runs may start within one hour.</param>
/// <param name="MaxRuntimeMinutes">How long one run may take, in minutes.</param>
public sealed record AutomationDraft(
    string Name,
    string Prompt,
    bool Enabled,
    string TimeZone,
    IReadOnlyList<AutomationTriggerDraft> Triggers,
    string TargetKind,
    IReadOnlyList<AutomationRepositoryDraft> Repositories,
    IReadOnlyList<string> SecretRefs,
    string EnvironmentId,
    string Model,
    string Mode,
    IReadOnlyList<string> AllowedTools,
    int MaxConcurrentRuns,
    int MaxRunsPerHour,
    int MaxRuntimeMinutes);
