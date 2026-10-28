using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Cloud;

/// <summary>The body of <c>POST /cloud/agents</c>. The shape follows the Cursor Cloud Agents API v1 where it fits.</summary>
public sealed record CreateCloudAgentRequest
{
    /// <summary>Gets the display name, 1 to 100 characters. When absent the agent is named after its first repository.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Gets the repositories the agent works in, 1 to 16. The first is the primary one: the agent starts there.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CloudRepository>? Repositories { get; init; }

    /// <summary>Gets the name of a saved environment definition to use instead of the repository's own <c>environment.json</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the names of the secrets (see <c>PUT /cloud/secrets/{name}</c>) to load into the agent's environment. Names only, never values.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SecretRefs { get; init; }

    /// <summary>Gets the minutes without activity after which the agent goes idle. When absent the server default (15) applies.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IdleMinutes { get; init; }

    /// <summary>Gets the model the agent should use, passed to the engine as a hint. Optional.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Gets the engine mode (for example <c>agent</c> or <c>plan</c>), passed as a hint. Optional.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }
}
