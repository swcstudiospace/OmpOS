using Autonogrammer.Harness.Automations.Scheduling;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Where an automation runs and what the run starts with. Secrets are named, never held: the cloud control plane resolves a name to
/// a value for the owner when the agent is provisioned.
/// </summary>
public sealed record AutomationTargetSpec
{
    /// <summary>Gets the kind of target.</summary>
    public AutomationTargetKind Kind { get; init; } = AutomationTargetKind.Cloud;

    /// <summary>Gets the repositories a cloud agent works in, 1 to 16 for a cloud target and none for a local one. The first is the primary one.</summary>
    public IReadOnlyList<AutomationRepository> Repositories { get; init; } = [];

    /// <summary>Gets the names of the secrets to load into a cloud agent's environment. Names only, never values.</summary>
    public IReadOnlyList<string> SecretRefs { get; init; } = [];

    /// <summary>Gets the name of a saved cloud environment definition, or null.</summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the model hint passed to the agent, or null.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the engine mode hint passed to the agent, or null.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets the surface a session target starts. Ignored unless <see cref="Kind"/> is <see cref="AutomationTargetKind.Session"/>.</summary>
    public ScheduledSessionSurface Surface { get; init; }

    /// <summary>Gets the optional project identifier of a session target.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the trusted folders of a session target. They stay on this device.</summary>
    public IReadOnlyList<string> TrustedFolders { get; init; } = [];

    /// <summary>Gets how a session target treats approval. Default is ask.</summary>
    public SessionApprovalMode ApprovalMode { get; init; }

    /// <summary>Gets the preset the editor turned into the cron trigger.</summary>
    public SchedulePreset Preset { get; init; }

    /// <summary>Gets what a session target does with a missed occurrence. Default is one late run. Non-session targets ignore this.</summary>
    public MissedRunPolicy MissedRunPolicy { get; init; } = MissedRunPolicy.RunOnce;

    /// <summary>Gets whether a per-session acknowledgement of Skip was recorded. It allows a save; the start still requires isolation.</summary>
    public bool SkipAcknowledged { get; init; }
}
