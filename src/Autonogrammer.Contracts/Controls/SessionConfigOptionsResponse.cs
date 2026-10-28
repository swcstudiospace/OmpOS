using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>The answer of <c>GET /sessions/{id}/config-options</c>.</summary>
public sealed record SessionConfigOptionsResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether the engine reported options for the session. When false, <see cref="Options"/> is empty and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a value indicating whether the options come from a live engine session. False means they are the last options an earlier run reported, and a change is refused until a run is live.</summary>
    public required bool Live { get; init; }

    /// <summary>Gets a plain sentence that says why the options are unavailable or not live, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets the options.</summary>
    public required IReadOnlyList<ConfigOption> Options { get; init; }
}
