using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Automations;

/// <summary>Where an automation runs and what the run starts with. Secrets are named, never carried.</summary>
public sealed record AutomationTargetInfo
{
    /// <summary>Gets the kind of target: <c>cloud</c> (a cloud agent) or <c>local</c> (a desktop session; the API host has none). Default <c>cloud</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    /// <summary>Gets the repositories a cloud agent works in, 1 to 16 for a cloud target.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AutomationRepositoryInfo>? Repositories { get; init; }

    /// <summary>Gets the names of the cloud secrets (see <c>PUT /cloud/secrets/{name}</c>) to load into the agent. Names only, never values.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SecretRefs { get; init; }

    /// <summary>Gets the name of a saved cloud environment definition.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the model hint passed to the agent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Gets the engine mode hint passed to the agent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }
}
