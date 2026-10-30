using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Parses a command hook's standard output. It accepts Cursor's flat form
/// (<c>{ "permission": "deny", "user_message": "..." }</c>) and the Claude Code forms Cursor imports: the nested
/// <c>hookSpecificOutput</c> object (<c>permissionDecision</c>, <c>permissionDecisionReason</c>, <c>updatedInput</c>,
/// <c>additionalContext</c>) and the legacy top-level <c>decision</c> / <c>reason</c>. Fields an event does not honour are
/// dropped. A permission value the event's schema does not accept makes the output invalid.
/// </summary>
public static class HookOutputParser
{
    private const int MaxEnvironmentEntries = 256;
    private const int MaxPluginPaths = 64;

    /// <summary>
    /// Parses standard output.
    /// </summary>
    /// <param name="standardOutput">The hook's standard output.</param>
    /// <param name="hookEvent">The Cursor event, or null for an <c>omp</c> section hook whose output is passed through.</param>
    /// <returns>The parse result. It is never null and never throws.</returns>
    public static HookOutputParse Parse(string standardOutput, HookEvent? hookEvent)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        if (string.IsNullOrWhiteSpace(standardOutput))
        {
            return HookOutputParse.EmptyOutput;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(standardOutput, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return HookOutputParse.Failure("The output is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HookOutputParse.Failure("The output must be a JSON object.");
            }

            return hookEvent is { } known
                ? ParseCursor(root, HookEvents.Info(known))
                : new HookOutputParse { Kind = HookOutputKind.Valid, OmpObject = root.Clone() };
        }
    }

    private static HookOutputParse ParseCursor(JsonElement root, HookEventInfo info)
    {
        var fields = new Fields();
        if (!fields.ReadFlat(root) || !fields.ReadNested(root) || !fields.ReadLegacyDecision(root, info.Event))
        {
            return HookOutputParse.Failure(fields.Problem ?? "The output does not match the hook schema.");
        }

        var verdict = fields.Verdict;
        if (verdict == HookVerdict.Ask && info.Honoured.HasFlag(HookOutputFields.Permission))
        {
            switch (info.AskHandling)
            {
                case HookAskHandling.Invalid:
                    return HookOutputParse.Failure($"The permission \"ask\" is not accepted by {info.Name}.");
                case HookAskHandling.AsAllow:
                    verdict = HookVerdict.Allow;
                    break;
                case HookAskHandling.AsDeny:
                    verdict = HookVerdict.Deny;
                    break;
                default:
                    break;
            }
        }

        var raw = new HookOutputParse
        {
            Kind = HookOutputKind.Valid,
            Verdict = verdict,
            UserMessage = fields.UserMessage,
            AgentMessage = fields.AgentMessage,
            UpdatedInput = fields.UpdatedInput,
            UpdatedMcpToolOutput = fields.UpdatedMcpToolOutput,
            AdditionalContext = fields.AdditionalContext,
            FollowupMessage = fields.FollowupMessage,
            Environment = fields.Env,
            ShouldContinue = fields.ShouldContinue,
            PluginPaths = fields.PluginPaths,
        };
        return Mask(raw, info.Honoured);
    }

    /// <summary>
    /// Drops every field of a parse result that an event does not honour.
    /// </summary>
    /// <param name="raw">The parse result with all fields.</param>
    /// <param name="honoured">The fields the event uses.</param>
    /// <returns>The result with only the honoured fields.</returns>
    public static HookOutputParse Mask(HookOutputParse raw, HookOutputFields honoured)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return raw with
        {
            Verdict = honoured.HasFlag(HookOutputFields.Permission) ? raw.Verdict : null,
            UserMessage = honoured.HasFlag(HookOutputFields.UserMessage) ? raw.UserMessage : null,
            AgentMessage = honoured.HasFlag(HookOutputFields.AgentMessage) ? raw.AgentMessage : null,
            UpdatedInput = honoured.HasFlag(HookOutputFields.UpdatedInput) ? raw.UpdatedInput : null,
            UpdatedMcpToolOutput = honoured.HasFlag(HookOutputFields.UpdatedMcpToolOutput) ? raw.UpdatedMcpToolOutput : null,
            AdditionalContext = honoured.HasFlag(HookOutputFields.AdditionalContext) ? raw.AdditionalContext : null,
            FollowupMessage = honoured.HasFlag(HookOutputFields.FollowupMessage) ? raw.FollowupMessage : null,
            Environment = honoured.HasFlag(HookOutputFields.Env) ? raw.Environment : new Dictionary<string, string>(StringComparer.Ordinal),
            ShouldContinue = honoured.HasFlag(HookOutputFields.Continue) ? raw.ShouldContinue : null,
            PluginPaths = honoured.HasFlag(HookOutputFields.PluginPaths) ? raw.PluginPaths : [],
        };
    }

    /// <summary>The fields of one output while it is read; any type mismatch records a problem.</summary>
    private sealed class Fields
    {
        public HookVerdict? Verdict { get; private set; }

        public string? UserMessage { get; private set; }

        public string? AgentMessage { get; private set; }

        public JsonElement? UpdatedInput { get; private set; }

        public JsonElement? UpdatedMcpToolOutput { get; private set; }

        public string? AdditionalContext { get; private set; }

        public string? FollowupMessage { get; private set; }

        public Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

        public bool? ShouldContinue { get; private set; }

        public List<string> PluginPaths { get; } = [];

        public string? Problem { get; private set; }

        public bool ReadFlat(JsonElement root)
        {
            return ReadVerdict(root, "permission")
                && ReadText(root, "user_message", value => UserMessage = value)
                && ReadText(root, "agent_message", value => AgentMessage = value)
                && ReadText(root, "additional_context", value => AdditionalContext = value)
                && ReadText(root, "followup_message", value => FollowupMessage = value)
                && ReadObject(root, "updated_input", value => UpdatedInput = value)
                && ReadObject(root, "updated_mcp_tool_output", value => UpdatedMcpToolOutput = value)
                && ReadBool(root, "continue")
                && ReadEnv(root)
                && ReadPluginPaths(root);
        }

        public bool ReadNested(JsonElement root)
        {
            if (!root.TryGetProperty("hookSpecificOutput", out var nested) || nested.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            var ok = true;
            if (Verdict is null && nested.TryGetProperty("permissionDecision", out _))
            {
                ok = ReadVerdict(nested, "permissionDecision");
            }

            if (ok && nested.TryGetProperty("permissionDecisionReason", out var reason) && reason.ValueKind == JsonValueKind.String)
            {
                AgentMessage ??= reason.GetString();
            }

            if (ok && UpdatedInput is null && nested.TryGetProperty("updatedInput", out _))
            {
                ok = ReadObject(nested, "updatedInput", value => UpdatedInput = value);
            }

            if (ok && AdditionalContext is null && nested.TryGetProperty("additionalContext", out _))
            {
                ok = ReadText(nested, "additionalContext", value => AdditionalContext = value);
            }

            return ok;
        }

        public bool ReadLegacyDecision(JsonElement root, HookEvent hookEvent)
        {
            if (!root.TryGetProperty("decision", out var decision) || decision.ValueKind != JsonValueKind.String)
            {
                return true;
            }

            var reason = root.TryGetProperty("reason", out var reasonValue) && reasonValue.ValueKind == JsonValueKind.String
                ? reasonValue.GetString()
                : null;
            var blocking = string.Equals(decision.GetString(), "block", StringComparison.Ordinal)
                || string.Equals(decision.GetString(), "deny", StringComparison.Ordinal);
            if (hookEvent is HookEvent.Stop or HookEvent.SubagentStop)
            {
                if (blocking && FollowupMessage is null)
                {
                    FollowupMessage = reason;
                }

                return true;
            }

            if (hookEvent == HookEvent.BeforeSubmitPrompt)
            {
                if (blocking && ShouldContinue is null)
                {
                    ShouldContinue = false;
                    UserMessage ??= reason;
                }

                return true;
            }

            if (Verdict is null)
            {
                if (blocking)
                {
                    Verdict = HookVerdict.Deny;
                    AgentMessage ??= reason;
                }
                else if (string.Equals(decision.GetString(), "approve", StringComparison.Ordinal)
                    || string.Equals(decision.GetString(), "allow", StringComparison.Ordinal))
                {
                    Verdict = HookVerdict.Allow;
                }
            }

            return true;
        }

        private bool ReadVerdict(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            switch (text)
            {
                case "allow":
                    Verdict = HookVerdict.Allow;
                    return true;
                case "ask":
                    Verdict = HookVerdict.Ask;
                    return true;
                case "deny":
                    Verdict = HookVerdict.Deny;
                    return true;
                default:
                    Problem = $"\"{name}\" must be \"allow\", \"deny\" or \"ask\".";
                    return false;
            }
        }

        private bool ReadText(JsonElement element, string name, Action<string?> assign)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                Problem = $"\"{name}\" must be a string.";
                return false;
            }

            assign(value.GetString());
            return true;
        }

        private bool ReadObject(JsonElement element, string name, Action<JsonElement> assign)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                Problem = $"\"{name}\" must be an object.";
                return false;
            }

            assign(value.Clone());
            return true;
        }

        private bool ReadBool(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                Problem = $"\"{name}\" must be true or false.";
                return false;
            }

            ShouldContinue = value.GetBoolean();
            return true;
        }

        private bool ReadEnv(JsonElement element)
        {
            if (!element.TryGetProperty("env", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                Problem = "\"env\" must be an object of string values.";
                return false;
            }

            foreach (var property in value.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || Env.Count >= MaxEnvironmentEntries
                    || property.Name.Length == 0 || property.Name.Contains('=', StringComparison.Ordinal))
                {
                    Problem = "\"env\" must map at most 256 variable names to string values.";
                    return false;
                }

                Env[property.Name] = property.Value.GetString() ?? string.Empty;
            }

            return true;
        }

        private bool ReadPluginPaths(JsonElement element)
        {
            if (!element.TryGetProperty("pluginPaths", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > MaxPluginPaths)
            {
                Problem = "\"pluginPaths\" must be an array of at most 64 strings.";
                return false;
            }

            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    Problem = "\"pluginPaths\" must contain only strings.";
                    return false;
                }

                PluginPaths.Add(item.GetString() ?? string.Empty);
            }

            return true;
        }
    }
}
