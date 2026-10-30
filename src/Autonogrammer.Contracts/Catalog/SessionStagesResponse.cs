using System.Text.Json.Serialization;

using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The answer of <c>GET /sessions/{id}/stages</c>: the latest event of every stage of the session.</summary>
public sealed record SessionStagesResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether plugin stages (planner, orchestrator, substrate, workflow engine) can be observed. When false, <see cref="Stages"/> holds the engine stage and one <see cref="StageStatus.Unavailable"/> stage for each plugin source, and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why plugin stages are unavailable, or null when they are available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets the stages: the latest event for each source and stage, in pipeline order.</summary>
    public required IReadOnlyList<StageEvent> Stages { get; init; }
}
