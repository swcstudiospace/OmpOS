using System.Collections.Frozen;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Reads the <c>hooks</c> key of a Claude Code <c>settings.json</c>: the nested layout
/// <c>{ "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "...", "timeout": 5 } ] } ] } }</c>.
/// Cursor imports exactly PreToolUse, PostToolUse, UserPromptSubmit, Stop and SubagentStop
/// (https://cursor.com/docs/reference/third-party-hooks); other Claude events are reported and ignored.
/// </summary>
internal static class ClaudeSettingsReader
{
    private static readonly FrozenDictionary<string, HookEvent> EventMap = new Dictionary<string, HookEvent>(StringComparer.Ordinal)
    {
        ["PreToolUse"] = HookEvent.PreToolUse,
        ["PostToolUse"] = HookEvent.PostToolUse,
        ["UserPromptSubmit"] = HookEvent.BeforeSubmitPrompt,
        ["Stop"] = HookEvent.Stop,
        ["SubagentStop"] = HookEvent.SubagentStop,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly TimeSpan ReplaceTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Reads the hooks of one settings file.
    /// </summary>
    /// <param name="root">The root of the settings JSON.</param>
    /// <param name="file">The file name for diagnostics.</param>
    /// <param name="diagnostics">Receives the problems found.</param>
    /// <returns>The hooks, with <c>loop_limit</c> fixed to null (no cap) as Cursor does for Claude hooks.</returns>
    public static HooksConfigDocument Read(JsonElement root, string file, List<HookDiagnostic> diagnostics)
    {
        var hooks = new List<HookDefinition>();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("hooks", out var section) || section.ValueKind == JsonValueKind.Null)
        {
            return Build(hooks, diagnostics);
        }

        if (section.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, "$.hooks", "\"hooks\" must be an object."));
            return Build(hooks, diagnostics);
        }

        if (!HookJsonReading.CheckNoDuplicateKeys(section, file, "$.hooks", diagnostics))
        {
            return Build([], diagnostics);
        }

        foreach (var eventProperty in section.EnumerateObject())
        {
            ReadEvent(eventProperty, file, diagnostics, hooks);
        }

        return Build(hooks.Take(HookConfigLimits.MaxHooksPerFile).ToList(), diagnostics);
    }

    private static void ReadEvent(JsonProperty eventProperty, string file, List<HookDiagnostic> diagnostics, List<HookDefinition> hooks)
    {
        var path = $"$.hooks.{eventProperty.Name}";
        if (!EventMap.TryGetValue(eventProperty.Name, out var hookEvent))
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Warning,
                file,
                path,
                $"The Claude hook event '{eventProperty.Name}' has no OmpOS equivalent and is not imported."));
            return;
        }

        if (eventProperty.Value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, path, "A Claude hook event maps to an array of matcher groups."));
            return;
        }

        var index = 0;
        var groupIndex = 0;
        foreach (var group in eventProperty.Value.EnumerateArray())
        {
            var groupPath = $"{path}[{groupIndex}]";
            groupIndex++;
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("hooks", out var inner) || inner.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, groupPath, "A matcher group is an object with a \"hooks\" array."));
                continue;
            }

            var matcher = ReadMatcher(group, hookEvent, file, groupPath, diagnostics);
            foreach (var item in inner.EnumerateArray())
            {
                var hook = ReadHook(item, HookEvents.Format(hookEvent), hookEvent, index, matcher, file, $"{groupPath}.hooks[{index}]", diagnostics);
                if (hook is not null)
                {
                    hooks.Add(hook);
                }

                index++;
            }
        }
    }

    private static string? ReadMatcher(JsonElement group, HookEvent hookEvent, string file, string groupPath, List<HookDiagnostic> diagnostics)
    {
        if (!group.TryGetProperty("matcher", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var matcher = value.GetString();
        if (hookEvent is not (HookEvent.PreToolUse or HookEvent.PostToolUse))
        {
            if (!HookMatcher.MatchesEverything(matcher))
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Warning,
                    file,
                    $"{groupPath}.matcher",
                    "Claude matchers only filter tool events; this matcher is ignored."));
            }

            return null;
        }

        return TranslateToolNames(matcher);
    }

    /// <summary>
    /// Rewrites Claude tool names in a matcher to Cursor's: <c>Bash</c> becomes <c>Shell</c> and <c>Edit</c> becomes
    /// <c>Write</c>. <c>Read</c>, <c>Write</c>, <c>Grep</c>, <c>Task</c>, <c>WebFetch</c> and <c>WebSearch</c> are the same
    /// in both. Claude's <c>Glob</c> has no Cursor match and is left as written.
    /// </summary>
    /// <param name="matcher">The matcher from the Claude file.</param>
    /// <returns>The matcher in Cursor tool names.</returns>
    internal static string? TranslateToolNames(string? matcher)
    {
        if (matcher is null)
        {
            return null;
        }

        var shell = Regex.Replace(matcher, @"\bBash\b", "Shell", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, ReplaceTimeout);
        return Regex.Replace(shell, @"\bEdit\b", "Write", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, ReplaceTimeout);
    }

    private static HookDefinition? ReadHook(
        JsonElement item,
        string eventName,
        HookEvent hookEvent,
        int index,
        string? matcher,
        string file,
        string path,
        List<HookDiagnostic> diagnostics)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, path, "A hook must be a JSON object."));
            return null;
        }

        var type = item.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String ? typeValue.GetString() : "command";
        if (!string.Equals(type, "command", StringComparison.Ordinal))
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Warning,
                file,
                $"{path}.type",
                $"The Claude hook type '{type}' is not imported; only command hooks are."));
            return null;
        }

        if (!item.TryGetProperty("command", out var commandValue) || commandValue.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(commandValue.GetString()))
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, $"{path}.command", "A command hook needs a non-empty \"command\" string."));
            return null;
        }

        double? timeout = null;
        if (item.TryGetProperty("timeout", out var timeoutValue) && timeoutValue.ValueKind == JsonValueKind.Number && timeoutValue.TryGetDouble(out var seconds) && seconds > 0)
        {
            timeout = Math.Min(seconds, HookConfigLimits.MaxTimeoutSeconds);
        }

        if (!HookMatcher.TryCompile(matcher, out _, out var problem))
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Warning,
                file,
                $"{path}.matcher",
                $"The matcher is not a valid regular expression ({problem}). The hook will never match."));
        }

        return new HookDefinition(
            eventName,
            hookEvent,
            false,
            index,
            HookType.Command,
            commandValue.GetString(),
            null,
            null,
            null,
            timeout,
            null,
            true,
            false,
            matcher,
            null,
            true,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            HookLocations.Both);
    }

    private static HooksConfigDocument Build(IReadOnlyList<HookDefinition> hooks, List<HookDiagnostic> diagnostics) =>
        new(HookConfigFormat.ClaudeSettings, 1, hooks, [], diagnostics);
}
