using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The wire form of a <see cref="HookDecision"/>: the Cursor-shaped merged answer in snake_case that the loopback bridge
/// returns to the omp extension. Every member is optional; an empty object means allow.
/// </summary>
public sealed class HookDecisionWire
{
    /// <summary>Gets or sets <c>permission</c>: <c>allow</c>, <c>ask</c> or <c>deny</c>.</summary>
    [JsonPropertyName("permission")]
    public string? Permission { get; set; }

    /// <summary>Gets or sets <c>user_message</c>.</summary>
    [JsonPropertyName("user_message")]
    public string? UserMessage { get; set; }

    /// <summary>Gets or sets <c>agent_message</c>.</summary>
    [JsonPropertyName("agent_message")]
    public string? AgentMessage { get; set; }

    /// <summary>Gets or sets <c>updated_input</c>.</summary>
    [JsonPropertyName("updated_input")]
    public JsonElement? UpdatedInput { get; set; }

    /// <summary>Gets or sets <c>additional_context</c>.</summary>
    [JsonPropertyName("additional_context")]
    public string? AdditionalContext { get; set; }

    /// <summary>Gets or sets <c>followup_message</c>.</summary>
    [JsonPropertyName("followup_message")]
    public string? FollowupMessage { get; set; }

    /// <summary>Gets or sets <c>continue</c>.</summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; set; }

    /// <summary>Gets or sets <c>updated_mcp_tool_output</c>.</summary>
    [JsonPropertyName("updated_mcp_tool_output")]
    public JsonElement? UpdatedMcpToolOutput { get; set; }

    /// <summary>Gets or sets <c>env</c>.</summary>
    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string>? Env { get; set; }

    /// <summary>Gets or sets <c>pluginPaths</c>.</summary>
    [JsonPropertyName("pluginPaths")]
    public IReadOnlyList<string>? PluginPaths { get; set; }

    /// <summary>Gets or sets <c>omp_result</c>: the merged omp extension result of <c>omp</c> section hooks.</summary>
    [JsonPropertyName("omp_result")]
    public JsonElement? OmpResult { get; set; }

    /// <summary>Gets or sets <c>hooks_run</c>: how many hooks started.</summary>
    [JsonPropertyName("hooks_run")]
    public int HooksRun { get; set; }

    /// <summary>Gets or sets <c>denial_cause</c>, the name of a <see cref="HookDecisionCause"/>.</summary>
    [JsonPropertyName("denial_cause")]
    public string? DenialCause { get; set; }
}
