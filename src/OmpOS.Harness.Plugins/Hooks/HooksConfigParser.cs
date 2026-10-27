using System.Collections.Frozen;
using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Parses hook configuration files: Cursor's <c>hooks.json</c> (with the OmpOS extras and the <c>omp</c> section) and the
/// <c>hooks</c> key of a Claude Code <c>settings.json</c>. Parsing never throws: a file that cannot be used becomes
/// diagnostics and an empty hook list.
/// </summary>
public static class HooksConfigParser
{
    private static readonly FrozenSet<string> OmpEvents = new[]
    {
        "tool_call", "tool_result", "input", "session_stop", "before_subagent_spawn", "session_before_compact",
        "auto_compaction_start", "session_start", "session_shutdown", "agent_end", "turn_end", "message_end",
        "tool_approval_requested", "tool_approval_resolved",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TopLevelKeys = new[] { "$schema", "version", "hooks", "omp" }
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The omp extension event names that the <c>omp</c> section accepts.
    /// </summary>
    public static IReadOnlyCollection<string> AllowedOmpEvents => OmpEvents;

    /// <summary>
    /// Parses one configuration file.
    /// </summary>
    /// <param name="utf8Json">The file bytes.</param>
    /// <param name="format">How to read the file.</param>
    /// <param name="file">The file name used in diagnostics.</param>
    /// <returns>The parsed file. It is never null; read <see cref="HooksConfigDocument.Diagnostics"/> for problems.</returns>
    public static HooksConfigDocument Parse(ReadOnlyMemory<byte> utf8Json, HookConfigFormat format, string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var diagnostics = new List<HookDiagnostic>();
        var limit = format == HookConfigFormat.HooksJson ? HookConfigLimits.MaxHooksFileBytes : HookConfigLimits.MaxClaudeSettingsBytes;
        if (utf8Json.Length > limit)
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Error,
                file,
                "$",
                $"The file is {utf8Json.Length} bytes; the limit is {limit} bytes."));
            return Empty(format, diagnostics);
        }

        var depth = format == HookConfigFormat.HooksJson ? HookConfigLimits.MaxHooksJsonDepth : HookConfigLimits.MaxClaudeSettingsDepth;
        using var document = HookJsonReading.TryParse(utf8Json, depth, file, diagnostics);
        if (document is null)
        {
            return Empty(format, diagnostics);
        }

        return format == HookConfigFormat.HooksJson
            ? ParseHooksJson(document.RootElement, file, diagnostics)
            : ClaudeSettingsReader.Read(document.RootElement, file, diagnostics);
    }

    private static HooksConfigDocument ParseHooksJson(JsonElement root, string file, List<HookDiagnostic> diagnostics)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, "$", "A hooks.json must be a JSON object."));
            return Empty(HookConfigFormat.HooksJson, diagnostics);
        }

        if (!HookJsonReading.CheckNoDuplicateKeys(root, file, "$", diagnostics))
        {
            return Empty(HookConfigFormat.HooksJson, diagnostics);
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!TopLevelKeys.Contains(property.Name))
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Warning,
                    file,
                    $"$.{property.Name}",
                    $"Unknown top-level key '{property.Name}' is ignored."));
            }
        }

        if (!TryReadVersion(root, file, diagnostics, out var version))
        {
            return Empty(HookConfigFormat.HooksJson, diagnostics);
        }

        var hooks = ReadSection(root, "hooks", isOmp: false, file, diagnostics);
        var ompHooks = ReadSection(root, "omp", isOmp: true, file, diagnostics);
        CheckHookCount(hooks.Count + ompHooks.Count, file, diagnostics);
        CheckDuplicateIds([.. hooks, .. ompHooks], file, diagnostics, out var kept);
        return new HooksConfigDocument(
            HookConfigFormat.HooksJson,
            version,
            [.. kept.Where(hook => !hook.IsOmpEvent)],
            [.. kept.Where(hook => hook.IsOmpEvent)],
            diagnostics);
    }

    private static bool TryReadVersion(JsonElement root, string file, List<HookDiagnostic> diagnostics, out int version)
    {
        version = 0;
        if (!root.TryGetProperty("version", out var value))
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, "$.version", "The \"version\" key is required; use 1."));
            return false;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out version) || version != 1)
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Error,
                file,
                "$.version",
                "Only \"version\": 1 is supported."));
            return false;
        }

        return true;
    }

    private static List<HookDefinition> ReadSection(JsonElement root, string key, bool isOmp, string file, List<HookDiagnostic> diagnostics)
    {
        var hooks = new List<HookDefinition>();
        if (!root.TryGetProperty(key, out var section) || section.ValueKind == JsonValueKind.Null)
        {
            return hooks;
        }

        if (section.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, $"$.{key}", $"\"{key}\" must be an object that maps event names to arrays."));
            return hooks;
        }

        foreach (var eventProperty in section.EnumerateObject())
        {
            ReadEvent(eventProperty, key, isOmp, file, diagnostics, hooks);
        }

        return hooks;
    }

    private static void ReadEvent(
        JsonProperty eventProperty,
        string key,
        bool isOmp,
        string file,
        List<HookDiagnostic> diagnostics,
        List<HookDefinition> hooks)
    {
        var eventName = eventProperty.Name;
        var path = $"$.{key}.{eventName}";
        HookEvent? hookEvent = null;
        if (isOmp)
        {
            if (!OmpEvents.Contains(eventName))
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Warning,
                    file,
                    path,
                    $"'{eventName}' is not an omp extension event OmpOS forwards; its hooks are ignored."));
                return;
            }
        }
        else if (HookEvents.TryParse(eventName, out var parsed))
        {
            hookEvent = parsed;
        }
        else
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Warning,
                file,
                path,
                $"'{eventName}' is not a known hook event. The hooks are kept and shown as an unknown event; they never run."));
        }

        if (eventProperty.Value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, path, "An event maps to an array of hooks."));
            return;
        }

        var index = 0;
        foreach (var item in eventProperty.Value.EnumerateArray())
        {
            var hook = HookDefinitionReader.Read(item, eventName, hookEvent, isOmp, index, file, $"{path}[{index}]", diagnostics);
            if (hook is not null)
            {
                hooks.Add(hook);
            }

            index++;
        }
    }

    private static void CheckHookCount(int count, string file, List<HookDiagnostic> diagnostics)
    {
        if (count > HookConfigLimits.MaxHooksPerFile)
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Error,
                file,
                "$",
                $"The file declares {count} hooks; the limit is {HookConfigLimits.MaxHooksPerFile}. Only the first {HookConfigLimits.MaxHooksPerFile} are used."));
        }
    }

    private static void CheckDuplicateIds(
        IReadOnlyList<HookDefinition> all,
        string file,
        List<HookDiagnostic> diagnostics,
        out List<HookDefinition> kept)
    {
        kept = [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hook in all.Take(HookConfigLimits.MaxHooksPerFile))
        {
            if (hook.Id is not null && !ids.Add(hook.Id))
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Error,
                    file,
                    $"$.{(hook.IsOmpEvent ? "omp" : "hooks")}.{hook.EventName}[{hook.Index}].id",
                    $"The id '{hook.Id}' is used by more than one hook in this file; the later hook is ignored."));
                continue;
            }

            kept.Add(hook);
        }
    }

    private static HooksConfigDocument Empty(HookConfigFormat format, List<HookDiagnostic> diagnostics) =>
        new(format, 1, [], [], diagnostics);
}
