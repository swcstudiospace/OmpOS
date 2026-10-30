using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The merged answer of every hook that ran for one event. Permission: any deny beats ask beats allow. User and agent
/// messages are concatenated in run order. For the other fields the last responding hook wins, which per the Cursor
/// documentation means a lower-priority source overrides a higher-priority one (owner decision O3, default).
/// </summary>
public sealed record HookDecision
{
    /// <summary>The decision of a dispatch where no hook ran: allow, no fields.</summary>
    public static HookDecision None { get; } = new();

    /// <summary>The merged permission, or null when no hook produced one.</summary>
    public HookVerdict? Verdict { get; init; }

    /// <summary>Why the dispatch denied, or <see cref="HookDecisionCause.None"/>.</summary>
    public HookDecisionCause Cause { get; init; }

    /// <summary>The user messages of the hooks, joined with a newline, or null.</summary>
    public string? UserMessage { get; init; }

    /// <summary>The agent messages of the hooks, joined with a newline, or null. On a deny it becomes the reason the model sees.</summary>
    public string? AgentMessage { get; init; }

    /// <summary>The replacement tool input of the last hook that gave one.</summary>
    public JsonElement? UpdatedInput { get; init; }

    /// <summary>The replacement MCP tool output of the last hook that gave one.</summary>
    public JsonElement? UpdatedMcpToolOutput { get; init; }

    /// <summary>The <c>additional_context</c> of every hook that gave one, joined with a newline, or null.</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>The follow-up message of the last hook that gave one, after the loop limit was applied, or null.</summary>
    public string? FollowupMessage { get; init; }

    /// <summary>The environment variables of the <c>sessionStart</c> hooks; later hooks override earlier ones.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The <c>continue</c> answer of <c>beforeSubmitPrompt</c>: false when any hook answered false, else true when any answered true, else null.</summary>
    public bool? ShouldContinue { get; init; }

    /// <summary>The plugin directories a <c>workspaceOpen</c> hook asked to load.</summary>
    public IReadOnlyList<string> PluginPaths { get; init; } = [];

    /// <summary>The merged result object of the <c>omp</c> section hooks, or null.</summary>
    public JsonElement? OmpResult { get; init; }

    /// <summary>One record for every hook that was considered (not for hooks whose matcher did not match), in run order.</summary>
    public IReadOnlyList<HookRunRecord> Records { get; init; } = [];

    /// <summary>How many hooks started a process or a model call.</summary>
    public int HooksRun { get; init; }

    /// <summary>True when the dispatch denies the action.</summary>
    public bool IsDenied => Verdict == HookVerdict.Deny;

    /// <summary>
    /// Converts the decision to its wire form, the Cursor-shaped snake_case JSON the bridge answers with.
    /// </summary>
    /// <returns>The wire object.</returns>
    public HookDecisionWire ToWire() => new()
    {
        Permission = Verdict switch
        {
            HookVerdict.Allow => "allow",
            HookVerdict.Ask => "ask",
            HookVerdict.Deny => "deny",
            _ => null,
        },
        UserMessage = UserMessage,
        AgentMessage = AgentMessage,
        UpdatedInput = UpdatedInput,
        AdditionalContext = AdditionalContext,
        FollowupMessage = FollowupMessage,
        Continue = ShouldContinue,
        UpdatedMcpToolOutput = UpdatedMcpToolOutput,
        Env = Environment.Count == 0 ? null : Environment,
        PluginPaths = PluginPaths.Count == 0 ? null : PluginPaths,
        OmpResult = OmpResult,
        HooksRun = HooksRun,
        DenialCause = Cause == HookDecisionCause.None ? null : Cause.ToString(),
    };

    /// <summary>
    /// Serializes <see cref="ToWire"/> as compact UTF-8 JSON.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToWireJson() => JsonSerializer.Serialize(ToWire(), HooksJsonContext.Default.HookDecisionWire);
}
