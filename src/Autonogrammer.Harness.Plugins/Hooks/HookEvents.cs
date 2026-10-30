using System.Collections.Frozen;
using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The catalogue of hook events: wire names, kinds, honoured output fields and matcher values. The facts come from
/// https://cursor.com/docs/hooks (read 2026-10-27) and design 08-hooks-design.md section 1.
/// </summary>
public static class HookEvents
{
    private const HookOutputFields Messages = HookOutputFields.UserMessage | HookOutputFields.AgentMessage;

    private static readonly IReadOnlyList<HookEventInfo> Table =
    [
        new(HookEvent.SessionStart, "sessionStart", HookEventKind.Control, HookOutputFields.Env | HookOutputFields.AdditionalContext, HookAskHandling.Ignored, null, null, true),
        new(HookEvent.SessionEnd, "sessionEnd", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, null, null, true),
        new(HookEvent.PreToolUse, "preToolUse", HookEventKind.Permission, HookOutputFields.Permission | Messages | HookOutputFields.UpdatedInput, HookAskHandling.AsAllow, null, "tool_name", true),
        new(HookEvent.PostToolUse, "postToolUse", HookEventKind.Control, HookOutputFields.AdditionalContext | HookOutputFields.UpdatedMcpToolOutput, HookAskHandling.Ignored, null, "tool_name", true),
        new(HookEvent.PostToolUseFailure, "postToolUseFailure", HookEventKind.Control, HookOutputFields.AdditionalContext, HookAskHandling.Ignored, null, "tool_name", true),
        new(HookEvent.SubagentStart, "subagentStart", HookEventKind.Permission, HookOutputFields.Permission | Messages, HookAskHandling.AsDeny, null, "subagent_type", true),
        new(HookEvent.SubagentStop, "subagentStop", HookEventKind.Control, HookOutputFields.FollowupMessage, HookAskHandling.Ignored, null, "subagent_type", true),
        new(HookEvent.BeforeShellExecution, "beforeShellExecution", HookEventKind.Permission, HookOutputFields.Permission | Messages, HookAskHandling.Enforced, null, "command", true),
        new(HookEvent.AfterShellExecution, "afterShellExecution", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, null, "command", true),
        new(HookEvent.BeforeMcpExecution, "beforeMCPExecution", HookEventKind.Permission, HookOutputFields.Permission | Messages, HookAskHandling.Enforced, null, "tool_name", true),
        new(HookEvent.AfterMcpExecution, "afterMCPExecution", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, null, "tool_name", true),
        new(HookEvent.BeforeReadFile, "beforeReadFile", HookEventKind.Permission, HookOutputFields.Permission | HookOutputFields.UserMessage, HookAskHandling.Invalid, "Read", null, true),
        new(HookEvent.AfterFileEdit, "afterFileEdit", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, "Write", null, true),
        new(HookEvent.BeforeSubmitPrompt, "beforeSubmitPrompt", HookEventKind.Control, HookOutputFields.Continue | HookOutputFields.UserMessage, HookAskHandling.Ignored, "UserPromptSubmit", null, true),
        new(HookEvent.PreCompact, "preCompact", HookEventKind.Observe, HookOutputFields.UserMessage, HookAskHandling.Ignored, null, null, true),
        new(HookEvent.Stop, "stop", HookEventKind.Control, HookOutputFields.FollowupMessage, HookAskHandling.Ignored, "Stop", null, true),
        new(HookEvent.AfterAgentResponse, "afterAgentResponse", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, "AgentResponse", null, true),
        new(HookEvent.AfterAgentThought, "afterAgentThought", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, "AgentThought", null, true),
        new(HookEvent.BeforeTabFileRead, "beforeTabFileRead", HookEventKind.Permission, HookOutputFields.Permission, HookAskHandling.Invalid, "TabRead", null, false),
        new(HookEvent.AfterTabFileEdit, "afterTabFileEdit", HookEventKind.Observe, HookOutputFields.None, HookAskHandling.Ignored, "TabWrite", null, false),
        new(HookEvent.WorkspaceOpen, "workspaceOpen", HookEventKind.Control, HookOutputFields.PluginPaths, HookAskHandling.Ignored, null, null, true),
    ];

    private static readonly FrozenDictionary<HookEvent, HookEventInfo> ByEvent =
        Table.ToFrozenDictionary(info => info.Event);

    private static readonly FrozenDictionary<string, HookEventInfo> ByName =
        Table.ToFrozenDictionary(info => info.Name, StringComparer.Ordinal);

    /// <summary>
    /// Every event, in the order of the <see cref="HookEvent"/> enum.
    /// </summary>
    public static IReadOnlyList<HookEventInfo> All => Table;

    /// <summary>
    /// Returns the facts about an event.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <returns>The facts.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined event.</exception>
    public static HookEventInfo Info(HookEvent hookEvent) =>
        ByEvent.TryGetValue(hookEvent, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(hookEvent), hookEvent, "Unknown hook event.");

    /// <summary>
    /// Returns the camelCase wire name of an event, for example <c>beforeMCPExecution</c>.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <returns>The wire name.</returns>
    public static string Format(HookEvent hookEvent) => Info(hookEvent).Name;

    /// <summary>
    /// Parses a wire name. The match is exact and case-sensitive, as in Cursor's <c>hooks.json</c>.
    /// </summary>
    /// <param name="name">The name from a hook file or a bridge request.</param>
    /// <param name="hookEvent">The event when the name is known.</param>
    /// <returns>True when the name is one of the 21 events.</returns>
    public static bool TryParse(string? name, out HookEvent hookEvent)
    {
        if (name is not null && ByName.TryGetValue(name, out var info))
        {
            hookEvent = info.Event;
            return true;
        }

        hookEvent = default;
        return false;
    }

    /// <summary>
    /// Returns the value a hook's matcher is tested against for one call of an event, or null when the event has no
    /// matcher value (then only an empty matcher or <c>*</c> matches).
    /// </summary>
    /// <remarks>
    /// <c>preToolUse</c>, <c>postToolUse</c> and <c>postToolUseFailure</c> test the tool type (<c>Shell</c>, <c>Read</c>,
    /// <c>Write</c>, <c>MCP:name</c>...), the subagent events the subagent type, the shell events the full command
    /// string. MCP events test <c>tool_name</c>: Cursor documents no matcher for them, so this is an Autonogrammer choice.
    /// </remarks>
    /// <param name="hookEvent">The event.</param>
    /// <param name="payload">The event-specific payload; a non-object payload yields no value.</param>
    /// <returns>The value to test, or null.</returns>
    public static string? MatcherValue(HookEvent hookEvent, JsonElement payload)
    {
        var info = Info(hookEvent);
        if (info.FixedMatcherValue is not null)
        {
            return info.FixedMatcherValue;
        }

        if (info.MatcherField is null || payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return payload.TryGetProperty(info.MatcherField, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// True when the event is one of the six permission events.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    public static bool IsPermission(HookEvent hookEvent) => Info(hookEvent).Kind == HookEventKind.Permission;
}
