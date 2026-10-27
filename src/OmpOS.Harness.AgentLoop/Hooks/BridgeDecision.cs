using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The merged decision of the hooks that ran for one request, in the Cursor-shaped snake_case form the extension reads. Every
/// member is optional on the wire: a member that is null is not written, and an object with nothing set means "allow, nothing
/// to do". The server resolves an <see cref="PermissionAsk"/> permission itself before it answers, so the extension only ever
/// sees <see cref="PermissionAllow"/>, <see cref="PermissionDeny"/> or no permission.
/// </summary>
public sealed record BridgeDecision
{
    /// <summary>The permission that lets the action proceed.</summary>
    public const string PermissionAllow = "allow";

    /// <summary>The permission that blocks the action.</summary>
    public const string PermissionDeny = "deny";

    /// <summary>The permission that asks the human to decide. The server resolves it through the approval prompt.</summary>
    public const string PermissionAsk = "ask";

    /// <summary>Gets the permission: <see cref="PermissionAllow"/>, <see cref="PermissionDeny"/>, <see cref="PermissionAsk"/>, or null for no opinion.</summary>
    [JsonPropertyName("permission")]
    public string? Permission { get; init; }

    /// <summary>Gets the text shown to the human.</summary>
    [JsonPropertyName("user_message")]
    public string? UserMessage { get; init; }

    /// <summary>Gets the text given to the model.</summary>
    [JsonPropertyName("agent_message")]
    public string? AgentMessage { get; init; }

    /// <summary>Gets the replacement tool input.</summary>
    [JsonPropertyName("updated_input")]
    public JsonElement? UpdatedInput { get; init; }

    /// <summary>Gets the extra context to hand to the model.</summary>
    [JsonPropertyName("additional_context")]
    public string? AdditionalContext { get; init; }

    /// <summary>Gets the follow-up message a stop hook asks the agent to continue with.</summary>
    [JsonPropertyName("followup_message")]
    public string? FollowupMessage { get; init; }

    /// <summary>Gets a value saying whether the agent should continue; false stops a prompt from being sent.</summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; init; }

    /// <summary>Gets the replacement output of an MCP tool.</summary>
    [JsonPropertyName("updated_mcp_tool_output")]
    public JsonElement? UpdatedMcpToolOutput { get; init; }

    /// <summary>Gets the environment variables a session-start hook sets. The extension ignores them; the host keeps them.</summary>
    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>Gets the result of the hooks in the <c>omp</c> section of the configuration, to merge into the omp event result.</summary>
    [JsonPropertyName("omp_result")]
    public JsonElement? OmpResult { get; init; }

    /// <summary>Gets the number of hooks that ran for the request.</summary>
    [JsonPropertyName("hooks_run")]
    public int HooksRun { get; init; }

    /// <summary>Gets a value saying that the handler failed and the request was let through because its event is not fail-closed.</summary>
    [JsonPropertyName("failed_open")]
    public bool FailedOpen { get; init; }
}
