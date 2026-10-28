using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Cloud;

/// <summary>
/// The durable record of one cloud agent, as <see cref="ICloudAgentStore"/> keeps it. It holds the provider references
/// (<see cref="WorkerRef"/>, <see cref="VolumeRef"/>) that the API never returns; <see cref="CloudAgentResponse"/> is the public view.
/// </summary>
public sealed record CloudAgentRecord
{
    /// <summary>Gets the agent identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the owner: the subject of the token that created the agent.</summary>
    public required string OwnerId { get; init; }

    /// <summary>Gets the identifier of the session that holds the agent's event log. It is also the worker token subject.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the lifecycle status.</summary>
    public required CloudAgentStatus Status { get; init; }

    /// <summary>Gets when the agent was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the record last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the repositories the agent works in. The first is the primary one.</summary>
    public required IReadOnlyList<CloudRepository> Repositories { get; init; }

    /// <summary>Gets the branch the agent pushes to.</summary>
    public required string Branch { get; init; }

    /// <summary>Gets the idle window in minutes.</summary>
    public required int IdleMinutes { get; init; }

    /// <summary>Gets when the agent last started or finished a run, or was created.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }

    /// <summary>Gets the saved environment definition the agent was created with, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the names of the secrets loaded into the agent.</summary>
    public IReadOnlyList<string> SecretRefs { get; init; } = [];

    /// <summary>Gets the model hint, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Gets the mode hint, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }

    /// <summary>Gets the provider identifier of the worker container, or null before it exists. Never returned by the API.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkerRef { get; init; }

    /// <summary>Gets the provider identifier of the worker volume, or null. Never returned by the API.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeRef { get; init; }

    /// <summary>Gets the engine's own session identifier, used to resume the conversation with <c>session/load</c>, or null before the first run.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeSessionId { get; init; }

    /// <summary>Gets the pull requests the agent opened.</summary>
    public IReadOnlyList<CloudPullRequest> PullRequests { get; init; } = [];

    /// <summary>Gets when the agent went idle, or null while it is not idle.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? IdleAt { get; init; }

    /// <summary>Gets when the current awake stretch began, or null while the worker is not counted as awake.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ActiveSince { get; init; }

    /// <summary>Gets the seconds of finished awake stretches.</summary>
    public double ComputeSeconds { get; init; }

    /// <summary>Gets why the agent failed or is orphaned, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CloudAgentError? Error { get; init; }

    /// <summary>Gets the Idempotency-Key the creating request carried, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdempotencyKey { get; init; }

    /// <summary>Gets the fingerprint of the creating request, compared when the same key arrives again.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestFingerprint { get; init; }
}
