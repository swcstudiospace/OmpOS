using System.Text.Json.Serialization;

using OmpOS.Contracts.Agents;

namespace OmpOS.Contracts.Catalog;

/// <summary>The answer of <c>GET /sessions/{id}/context</c>: the task context of the session.</summary>
public sealed record SessionContextResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether a task-context source answered. When false, <see cref="Context"/> is null and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why the context is unavailable, or null when it is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets the task context, or null when it is unavailable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TaskContext? Context { get; init; }
}
