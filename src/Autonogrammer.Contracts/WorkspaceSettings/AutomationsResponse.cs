using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /automations</c>: the automations the host configured. The API starts no run.</summary>
public sealed record AutomationsResponse
{
    /// <summary>Gets a value indicating whether this API has an automation engine configured.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why automations are unavailable, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets the automations; empty when unavailable.</summary>
    public required IReadOnlyList<WorkspaceAutomationInfo> Automations { get; init; }
}
