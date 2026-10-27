using System.Collections.Frozen;
using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Reads one hook object of a <c>hooks.json</c> into a <see cref="HookDefinition"/>. A hook with an error is dropped and
/// the error is recorded; a hook with only warnings is kept.
/// </summary>
internal static class HookDefinitionReader
{
    private static readonly FrozenSet<string> KnownFields = new[]
    {
        "command", "type", "prompt", "model", "timeout", "loop_limit", "failClosed", "matcher",
        "args", "id", "enabled", "description", "env", "runsIn", "blocking",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Reads one hook object.
    /// </summary>
    /// <param name="element">The hook object.</param>
    /// <param name="eventName">The event name the hook is filed under.</param>
    /// <param name="hookEvent">The parsed event, or null.</param>
    /// <param name="isOmpEvent">True for a hook of the <c>omp</c> section.</param>
    /// <param name="index">The position in the event's array.</param>
    /// <param name="file">The file name for diagnostics.</param>
    /// <param name="path">The JSON path of the hook.</param>
    /// <param name="diagnostics">Receives the problems found.</param>
    /// <returns>The hook, or null when it has an error.</returns>
    public static HookDefinition? Read(
        JsonElement element,
        string eventName,
        HookEvent? hookEvent,
        bool isOmpEvent,
        int index,
        string file,
        string path,
        List<HookDiagnostic> diagnostics)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Error(diagnostics, file, path, "A hook must be a JSON object.");
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!KnownFields.Contains(property.Name))
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Warning,
                    file,
                    $"{path}.{property.Name}",
                    $"Unknown hook field '{property.Name}' is ignored."));
            }
        }

        var errors = diagnostics.Count(d => d.Severity == HookDiagnosticSeverity.Error);
        var reader = new FieldReader(element, file, path, diagnostics);
        var type = ReadType(reader);
        var command = reader.Text("command");
        var args = reader.StringArray("args");
        var prompt = reader.Text("prompt");
        var timeout = ReadTimeout(reader);
        var (loopLimit, loopSpecified) = ReadLoopLimit(reader, hookEvent);
        var failClosed = reader.Flag("failClosed", false);
        var matcher = reader.Text("matcher");
        var enabled = reader.Flag("enabled", true);
        var env = reader.StringMap("env");
        var runsIn = ReadRunsIn(reader);
        var model = reader.Text("model");
        var id = reader.Text("id");
        var description = reader.Text("description");
        CheckBody(reader, type, command, args, prompt);
        CheckMatcher(matcher, file, path, diagnostics);

        if (diagnostics.Count(d => d.Severity == HookDiagnosticSeverity.Error) != errors)
        {
            return null;
        }

        return new HookDefinition(
            eventName,
            hookEvent,
            isOmpEvent,
            index,
            type,
            command,
            args,
            prompt,
            model,
            timeout,
            loopLimit,
            loopSpecified,
            failClosed,
            matcher,
            id,
            enabled,
            description,
            env,
            runsIn);
    }

    private static HookType ReadType(FieldReader reader)
    {
        var text = reader.Text("type");
        if (text is null || string.Equals(text, "command", StringComparison.Ordinal))
        {
            return HookType.Command;
        }

        if (string.Equals(text, "prompt", StringComparison.Ordinal))
        {
            return HookType.Prompt;
        }

        reader.Error("type", $"The hook type '{text}' is not supported. Use \"command\" or \"prompt\".");
        return HookType.Command;
    }

    private static double? ReadTimeout(FieldReader reader)
    {
        var value = reader.Number("timeout");
        if (value is null)
        {
            return null;
        }

        if (value <= 0 || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            reader.Error("timeout", "The timeout must be a positive number of seconds.");
            return null;
        }

        if (value > HookConfigLimits.MaxTimeoutSeconds)
        {
            reader.Warning("timeout", $"The timeout is clamped to {(int)HookConfigLimits.MaxTimeoutSeconds} seconds.");
            return HookConfigLimits.MaxTimeoutSeconds;
        }

        return value;
    }

    private static (int? Limit, bool Specified) ReadLoopLimit(FieldReader reader, HookEvent? hookEvent)
    {
        if (!reader.Has("loop_limit"))
        {
            return (null, false);
        }

        if (hookEvent is not (HookEvent.Stop or HookEvent.SubagentStop))
        {
            reader.Warning("loop_limit", "loop_limit only applies to stop and subagentStop hooks and is ignored here.");
        }

        if (reader.IsNull("loop_limit"))
        {
            return (null, true);
        }

        var value = reader.Number("loop_limit");
        if (value is null || value < 0 || Math.Floor(value.Value) != value.Value || value > int.MaxValue)
        {
            reader.Error("loop_limit", "loop_limit must be a non-negative integer or null.");
            return (null, true);
        }

        return ((int)value.Value, true);
    }

    private static HookLocations ReadRunsIn(FieldReader reader)
    {
        var names = reader.StringArray("runsIn");
        if (names is null)
        {
            return HookLocations.Both;
        }

        var locations = HookLocations.None;
        foreach (var name in names)
        {
            switch (name)
            {
                case "local":
                    locations |= HookLocations.Local;
                    break;
                case "remote":
                    locations |= HookLocations.Remote;
                    break;
                default:
                    reader.Error("runsIn", $"runsIn lists \"local\" and \"remote\"; '{name}' is not one of them.");
                    break;
            }
        }

        return locations;
    }

    private static void CheckBody(FieldReader reader, HookType type, string? command, IReadOnlyList<string>? args, string? prompt)
    {
        if (type == HookType.Prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                reader.Error("prompt", "A prompt hook needs a non-empty \"prompt\".");
            }

            if (command is not null || args is not null)
            {
                reader.Warning("command", "A prompt hook does not run a command; command and args are ignored.");
            }

            return;
        }

        if (command is not null && args is not null)
        {
            reader.Error("command", "A hook sets either \"command\" or \"args\", not both.");
        }
        else if (command is null && args is null)
        {
            reader.Error("command", "A command hook needs a \"command\" string or an \"args\" array.");
        }
        else if (command is not null && string.IsNullOrWhiteSpace(command))
        {
            reader.Error("command", "The command must not be empty.");
        }
        else if (args is not null && (args.Count == 0 || string.IsNullOrEmpty(args[0])))
        {
            reader.Error("args", "args needs at least the executable as its first element.");
        }
    }

    private static void CheckMatcher(string? matcher, string file, string path, List<HookDiagnostic> diagnostics)
    {
        if (!HookMatcher.TryCompile(matcher, out _, out var problem))
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Warning,
                file,
                $"{path}.matcher",
                $"The matcher is not a valid regular expression ({problem}). The hook will never match, and a failClosed hook will block."));
        }
    }

    private static void Error(List<HookDiagnostic> diagnostics, string file, string path, string message) =>
        diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, path, message));

    /// <summary>Typed access to the fields of one hook object that records a diagnostic for a wrong type.</summary>
    private sealed class FieldReader(JsonElement element, string file, string path, List<HookDiagnostic> diagnostics)
    {
        public bool Has(string name) => element.TryGetProperty(name, out _);

        public bool IsNull(string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null;

        public string? Text(string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                Error(name, $"\"{name}\" must be a string.");
                return null;
            }

            var text = value.GetString();
            if (text is not null && text.Length > HookConfigLimits.MaxStringLength)
            {
                Error(name, $"\"{name}\" is longer than {HookConfigLimits.MaxStringLength} characters.");
                return null;
            }

            return text;
        }

        public double? Number(string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            {
                Error(name, $"\"{name}\" must be a number.");
                return null;
            }

            return number;
        }

        public bool Flag(string name, bool fallback)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            Error(name, $"\"{name}\" must be true or false.");
            return fallback;
        }

        public IReadOnlyList<string>? StringArray(string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > HookConfigLimits.MaxArguments)
            {
                Error(name, $"\"{name}\" must be an array of at most {HookConfigLimits.MaxArguments} strings.");
                return null;
            }

            var items = new List<string>(value.GetArrayLength());
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || (item.GetString()?.Length ?? 0) > HookConfigLimits.MaxStringLength)
                {
                    Error(name, $"\"{name}\" must contain only strings of at most {HookConfigLimits.MaxStringLength} characters.");
                    return null;
                }

                items.Add(item.GetString() ?? string.Empty);
            }

            return items;
        }

        public IReadOnlyDictionary<string, string> StringMap(string name)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return map;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                Error(name, $"\"{name}\" must be an object of string values.");
                return map;
            }

            foreach (var property in value.EnumerateObject())
            {
                var text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                if (text is null || text.Length > HookConfigLimits.MaxStringLength || property.Name.Length == 0
                    || property.Name.Contains('=', StringComparison.Ordinal))
                {
                    Error(name, $"\"{name}\" must map variable names to string values; '{property.Name}' is not valid.");
                    continue;
                }

                map[property.Name] = text;
            }

            return map;
        }

        public void Error(string name, string message) =>
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Error, file, $"{path}.{name}", message));

        public void Warning(string name, string message) =>
            diagnostics.Add(new HookDiagnostic(HookDiagnosticSeverity.Warning, file, $"{path}.{name}", message));
    }
}
