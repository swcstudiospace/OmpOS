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
}
