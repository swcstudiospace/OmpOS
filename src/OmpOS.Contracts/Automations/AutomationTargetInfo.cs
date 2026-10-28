namespace OmpOS.Contracts.Automations;

/// <summary>Where an automation runs and what the run starts with. Secrets are named, never carried.</summary>
public sealed record AutomationTargetInfo
{
    /// <summary>Gets the kind of target: <c>cloud</c> (a cloud agent) or <c>local</c> (a desktop session; the API host has none). Default <c>cloud</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the repositories a cloud agent works in, 1 to 16 for a cloud target.</summary>
    public IReadOnlyList<AutomationRepositoryInfo>? Repositories { get; init; }

    /// <summary>Gets the names of the cloud secrets (see <c>PUT /cloud/secrets/{name}</c>) to load into the agent. Names only, never values.</summary>
    public IReadOnlyList<string>? SecretRefs { get; init; }

    /// <summary>Gets the name of a saved cloud environment definition.</summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the model hint passed to the agent.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the engine mode hint passed to the agent.</summary>
    public string? Mode { get; init; }
}
