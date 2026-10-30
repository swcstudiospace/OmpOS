using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /hooks</c>: the effective hooks, the files they came from, the loading problems and the consent state. The API never starts a hook.</summary>
public sealed record HooksResponse
{
    /// <summary>Gets a value indicating whether the API has a workspace folder whose hooks it could read.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why the hooks are unavailable, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>Gets a value indicating whether the API knows the recorded consent of each hook. It is false: consent is recorded by the head that runs the hook, so <see cref="WorkspaceHookInfo.PreConsented"/> is the only consent the API reports.</summary>
    public required bool ConsentRecorded { get; init; }

    /// <summary>Gets every hook in merge order, including disabled hooks.</summary>
    public required IReadOnlyList<WorkspaceHookInfo> Hooks { get; init; }

    /// <summary>Gets the files the hooks were read from, in merge order.</summary>
    public required IReadOnlyList<WorkspaceHookSourceInfo> Sources { get; init; }

    /// <summary>Gets the problems found while loading.</summary>
    public required IReadOnlyList<WorkspaceHookDiagnosticInfo> Diagnostics { get; init; }
}
