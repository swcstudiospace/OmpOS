using System.Collections.Frozen;
using System.Text.Json;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// Validates a plugin manifest. The checks run in this order: size, JSON syntax, field names (unknown and repeated names
/// are rejected, with their location), typed deserialization, then value rules. Nothing in a manifest is executed.
/// </summary>
public static class PluginManifestParser
{
    /// <summary>
    /// The largest manifest accepted, in bytes. A larger manifest is rejected before it is parsed.
    /// </summary>
    public const int MaxManifestBytes = 64 * 1024;

    private const int MaxJsonDepth = 16;
    private const int MaxNameLength = 64;
    private const int MaxVersionLength = 32;
    private const int MaxListEntries = 64;
    private const int MaxHooks = 32;
    private const int MaxCommandArguments = 64;
    private const int MaxArgumentLength = 4096;
    private const int MaxPathLength = 4096;

    private static readonly FrozenSet<string> ManifestFields =
        new[] { "id", "version", "apiVersion", "capabilities", "permissions", "hooks" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> HookFields =
        new[] { "trigger", "command", "workingDirectory", "blocking" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Validates a UTF-8 JSON manifest and returns its typed form.
    /// </summary>
    /// <param name="utf8Json">The manifest bytes, at most <see cref="MaxManifestBytes"/> long.</param>
    /// <returns>The validated manifest.</returns>
    /// <exception cref="PluginManifestException">The manifest is rejected. <see cref="PluginManifestException.Kind"/> names the rule.</exception>
    public static PluginManifest Parse(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.Length > MaxManifestBytes)
        {
            throw new PluginManifestException(
                PluginRejectionKind.ManifestTooLarge,
                null,
                $"The manifest is {utf8Json.Length} bytes; the limit is {MaxManifestBytes} bytes.");
        }

        using var document = ParseDocument(utf8Json);
        var root = document.RootElement;
        RequireObject(root, "The manifest");
        RequireKnownFields(root, ManifestFields, "$");

        if (root.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var hook in hooks.EnumerateArray())
            {
                if (hook.ValueKind == JsonValueKind.Object)
                {
                    RequireKnownFields(hook, HookFields, $"$.hooks[{index}]");
                }

                index++;
            }
        }

        return Validate(DeserializeDocument(utf8Json));
    }

    private static JsonDocument ParseDocument(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            return JsonDocument.Parse(utf8Json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
        }
        catch (JsonException ex)
        {
            throw new PluginManifestException(
                PluginRejectionKind.ManifestMalformed,
                null,
                $"The manifest is not valid JSON: {ex.Message}",
                ex);
        }
    }

    private static void RequireObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new PluginManifestException(PluginRejectionKind.ManifestMalformed, null, $"{description} must be a JSON object.");
        }
    }

    private static void RequireKnownFields(JsonElement obj, FrozenSet<string> allowed, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new PluginManifestException(
                    PluginRejectionKind.DuplicateField,
                    null,
                    $"The field '{property.Name}' appears more than once at {location}.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw new PluginManifestException(
                    PluginRejectionKind.UnknownField,
                    null,
                    $"Unknown field '{property.Name}' at {location}. The manifest schema does not define it.");
            }
        }
    }

    private static ManifestDocument DeserializeDocument(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            return JsonSerializer.Deserialize(utf8Json.Span, PluginJsonContext.Default.ManifestDocument)
                ?? throw new PluginManifestException(PluginRejectionKind.ManifestMalformed, null, "The manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new PluginManifestException(
                PluginRejectionKind.ManifestMalformed,
                null,
                $"The manifest does not match the schema: {ex.Message}",
                ex);
        }
    }

    private static PluginManifest Validate(ManifestDocument document)
    {
        var id = document.Id;
        if (id is null || !IsValidName(id))
        {
            throw Invalid(null, $"The 'id' field must be 1 to {MaxNameLength} letters, digits, '.', '-' or '_', starting with a letter or digit.");
        }

        var version = document.Version;
        if (version is null || version.Length > MaxVersionLength || !Version.TryParse(version, out _))
        {
            throw Invalid(id, "The 'version' field must be a dotted numeric version such as 1.0.0.");
        }

        var apiVersion = document.ApiVersion ?? 0;
        if (apiVersion < 1)
        {
            throw Invalid(id, "The 'apiVersion' field must be an integer of at least 1.");
        }

        var capabilities = ValidateNames(document.Capabilities, "capabilities", id);
        var permissions = ValidateNames(document.Permissions, "permissions", id);
        var hooks = ValidateHooks(document.Hooks, id);
        return new PluginManifest(id, version, apiVersion, capabilities, permissions, hooks);
    }

    private static List<string> ValidateNames(List<string?>? names, string field, string id)
    {
        if (names is null)
        {
            throw Invalid(id, $"The '{field}' field must be an array.");
        }

        if (names.Count > MaxListEntries)
        {
            throw Invalid(id, $"The '{field}' field lists more than {MaxListEntries} entries.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(names.Count);
        foreach (var name in names)
        {
            if (name is null || !IsValidName(name))
            {
                throw Invalid(id, $"Each '{field}' entry must be 1 to {MaxNameLength} letters, digits, '.', '-' or '_'.");
            }

            if (!seen.Add(name))
            {
                throw Invalid(id, $"The '{field}' entry '{name}' is listed more than once.");
            }

            result.Add(name);
        }

        return result;
    }

    private static List<PluginHookDeclaration> ValidateHooks(List<HookDocument?>? hooks, string id)
    {
        if (hooks is null)
        {
            throw Invalid(id, "The 'hooks' field must be an array.");
        }

        if (hooks.Count > MaxHooks)
        {
            throw Invalid(id, $"The 'hooks' field declares more than {MaxHooks} hooks.");
        }

        var result = new List<PluginHookDeclaration>(hooks.Count);
        foreach (var hook in hooks)
        {
            if (hook is null)
            {
                throw Invalid(id, "Each hook must be a JSON object.");
            }

            var trigger = ParseTrigger(hook.Trigger, id);
            var command = ValidateCommand(hook.Command, hook.WorkingDirectory, id);
            result.Add(new PluginHookDeclaration(trigger, command, hook.Blocking));
        }

        return result;
    }

    private static HookTrigger ParseTrigger(string? value, string id)
    {
        switch (value)
        {
            case "sessionStart":
                return HookTrigger.SessionStart;
            case "beforeAction":
                return HookTrigger.BeforeAction;
            case "afterAction":
                return HookTrigger.AfterAction;
            case "sessionEnd":
                return HookTrigger.SessionEnd;
            default:
                throw Invalid(id, $"Unknown hook trigger '{value}'. Use sessionStart, beforeAction, afterAction or sessionEnd.");
        }
    }

    private static HookCommand ValidateCommand(List<string?>? arguments, string? workingDirectory, string id)
    {
        if (arguments is null || arguments.Count is 0 || arguments.Count > MaxCommandArguments)
        {
            throw Invalid(id, $"The hook 'command' must be an array of 1 to {MaxCommandArguments} strings.");
        }

        var result = new List<string>(arguments.Count);
        foreach (var argument in arguments)
        {
            if (argument is null || argument.Length > MaxArgumentLength || argument.Contains('\0', StringComparison.Ordinal))
            {
                throw Invalid(id, $"Each hook command argument must be a string of at most {MaxArgumentLength} characters without NUL.");
            }

            result.Add(argument);
        }

        if (string.IsNullOrWhiteSpace(result[0]))
        {
            throw Invalid(id, "The first hook command argument, the executable, must not be blank.");
        }

        if (workingDirectory is null
            || workingDirectory.Length > MaxPathLength
            || workingDirectory.Contains('\0', StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(workingDirectory))
        {
            throw Invalid(id, "The hook 'workingDirectory' must be an absolute path without NUL characters.");
        }

        return new HookCommand(result, workingDirectory);
    }

    private static bool IsValidName(string value)
    {
        if (value.Length is 0 or > MaxNameLength)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var allowed = char.IsAsciiLetterOrDigit(c) || (i > 0 && (c is '.' or '-' or '_'));
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static PluginManifestException Invalid(string? pluginId, string message) =>
        new(PluginRejectionKind.InvalidManifest, pluginId, message);
}
