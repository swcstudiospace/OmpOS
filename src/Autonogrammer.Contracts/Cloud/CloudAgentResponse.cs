using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cloud;

/// <summary>One cloud agent as the API shows it. No field holds a secret, a token or a provider identifier.</summary>
public sealed record CloudAgentResponse
{
    /// <summary>Gets the agent identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the lifecycle status.</summary>
    public required CloudAgentStatus Status { get; init; }

    /// <summary>Gets when the agent was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the repositories the agent works in.</summary>
    public required IReadOnlyList<CloudRepository> Repositories { get; init; }

    /// <summary>Gets an opaque reference to the worker, or null while none exists. It is not a provider identifier and cannot be used to reach the worker.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkerRef { get; init; }

    /// <summary>Gets the branch the agent pushes to in every repository.</summary>
    public required string Branch { get; init; }

    /// <summary>Gets the identifier of the session that holds the agent's event log: <c>GET /sessions/{sessionId}</c> reads it, and <c>GET /cloud/agents/{id}/events</c> streams it.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the pull requests the agent opened. Empty until one is.</summary>
    public required IReadOnlyList<CloudPullRequest> PullRequests { get; init; }

    /// <summary>Gets the most recent pull request, or null. It mirrors the Cursor API's single <c>pullRequest</c> member.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CloudPullRequest? PullRequest { get; init; }

    /// <summary>Gets when the agent last started or finished a run, or was created.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }

    /// <summary>Gets when the agent went idle, or null while it is not idle.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? IdleAt { get; init; }

    /// <summary>Gets the minutes without activity after which the agent goes idle.</summary>
    public required int IdleMinutes { get; init; }

    /// <summary>Gets the seconds the worker was awake for this agent, including the current stretch.</summary>
    public required double ComputeSeconds { get; init; }

    /// <summary>Gets the names of the secrets loaded into the agent. Never values.</summary>
    public required IReadOnlyList<string> SecretRefs { get; init; }

    /// <summary>Gets the model hint, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Gets the mode hint, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }

    /// <summary>Gets why the agent failed or is orphaned, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CloudAgentError? Error { get; init; }
}
