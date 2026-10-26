using System.Collections.Frozen;
using System.Text.Json;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp.AcpProtocol;

namespace OmpOS.Harness.Acp;

/// <summary>Maps ACP wire payloads to harness events and capability flags.</summary>
internal static class AcpWireMapping
{
    /// <summary>
    /// Maps one <c>session/update</c> payload. Returns <see langword="null"/> for kinds the harness does not model
    /// (for example <c>user_message_chunk</c> or <c>available_commands_update</c>); ACP reserves room for new kinds.
    /// </summary>
    /// <param name="update">The raw update object.</param>
    /// <returns>The mapped event, or <see langword="null"/>.</returns>
    /// <exception cref="JsonException">The update does not have the shape of its declared kind.</exception>
    public static HarnessEvent? MapSessionUpdate(JsonElement update)
    {
        var header = update.Deserialize(AcpJsonContext.Default.SessionUpdateHeader);
        return header?.SessionUpdate switch
        {
            "agent_message_chunk" => TextOf(update) is { } text ? new TextDeltaEvent(text) : null,
            "agent_thought_chunk" => TextOf(update) is { } thought ? new ThoughtEvent(thought) : null,
            "tool_call" => ToolCall(update),
            "tool_call_update" => ToolCallUpdate(update),
            "plan" => Plan(update),
            _ => null,
        };
    }

    /// <summary>Flattens the agent capabilities into dotted keys. Boolean flags map to <c>true</c>/<c>false</c>; object capabilities map to <c>true</c> when present.</summary>
    /// <param name="capabilities">The capabilities reported in <c>initialize</c>.</param>
    /// <returns>The flattened capability dictionary.</returns>
    public static IReadOnlyDictionary<string, string> Flatten(AcpFrames.AgentCapabilities capabilities)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["loadSession"] = Flag(capabilities.LoadSession),
            ["mcpCapabilities.http"] = Flag(capabilities.McpCapabilities.Http),
            ["mcpCapabilities.sse"] = Flag(capabilities.McpCapabilities.Sse),
            ["promptCapabilities.embeddedContext"] = Flag(capabilities.PromptCapabilities.EmbeddedContext),
            ["promptCapabilities.image"] = Flag(capabilities.PromptCapabilities.Image),
        };

        var session = capabilities.SessionCapabilities;
        AddPresence(map, "sessionCapabilities.list", session.List);
        AddPresence(map, "sessionCapabilities.fork", session.Fork);
        AddPresence(map, "sessionCapabilities.resume", session.Resume);
        AddPresence(map, "sessionCapabilities.close", session.Close);
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Validates the configured MCP servers and maps them to their wire entries. Name/value pairs are written in ordinal
    /// name order so the frame is deterministic. Exception messages name the offending server and never include a URL,
    /// argument, header value or environment value, because those are secrets.
    /// </summary>
    /// <param name="servers">The configured servers.</param>
    /// <returns>The wire entries, in configuration order; an empty array when none are configured.</returns>
    /// <exception cref="ArgumentException">A server is null, unnamed, duplicated, or lacks its command or an absolute http(s) URL.</exception>
    public static AcpFrames.McpServerFrame[] ToMcpFrames(IReadOnlyList<AcpMcpServer> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        if (servers.Count == 0)
        {
            return [];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var frames = new AcpFrames.McpServerFrame[servers.Count];
        for (var i = 0; i < servers.Count; i++)
        {
            var server = servers[i] ?? throw new ArgumentException($"MCP server entry {i} is null.", nameof(servers));
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new ArgumentException($"MCP server entry {i} has no name.", nameof(servers));
            }

            if (!names.Add(server.Name))
            {
                throw new ArgumentException($"MCP server name '{server.Name}' is configured more than once.", nameof(servers));
            }

            frames[i] = server switch
            {
                AcpStdioMcpServer stdio => Stdio(stdio),
                AcpHttpMcpServer http => Remote(http.Name, "http", http.Url, http.Headers),
                AcpSseMcpServer sse => Remote(sse.Name, "sse", sse.Url, sse.Headers),
                _ => throw new ArgumentException($"MCP server '{server.Name}' has an unsupported kind.", nameof(servers)),
            };
        }

        return frames;
    }

    /// <summary>Renders plan entries as a markdown checklist.</summary>
    /// <param name="entries">The plan entries.</param>
    /// <returns>One checklist line per entry.</returns>
    public static string RenderPlan(IEnumerable<AcpFrames.PlanEntry> entries) =>
        string.Join('\n', entries.Select(static entry => $"- {Box(entry.Status)} {entry.Content}"));

    private static AcpFrames.McpServerFrame Stdio(AcpStdioMcpServer server)
    {
        if (string.IsNullOrWhiteSpace(server.Command))
        {
            throw new ArgumentException($"MCP server '{server.Name}' has no command.", nameof(server));
        }

        if (server.Args is null || server.Args.Any(static argument => argument is null))
        {
            throw new ArgumentException($"MCP server '{server.Name}' has a missing argument list or a null argument.", nameof(server));
        }

        return new AcpFrames.McpServerFrame
        {
            Name = server.Name,
            Command = server.Command,
            Args = server.Args.ToArray(),
            Env = Pairs(server.Name, server.Env, "environment"),
        };
    }

    private static AcpFrames.McpServerFrame Remote(string name, string type, Uri url, IReadOnlyDictionary<string, string> headers)
    {
        if (url is null || !url.IsAbsoluteUri || !(string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"MCP server '{name}' needs an absolute http or https URL.", nameof(url));
        }

        return new AcpFrames.McpServerFrame
        {
            Type = type,
            Name = name,
            Url = url,
            Headers = Pairs(name, headers, "headers"),
        };
    }

    private static AcpFrames.NameValueFrame[] Pairs(string serverName, IReadOnlyDictionary<string, string> values, string what)
    {
        if (values is null)
        {
            throw new ArgumentException($"MCP server '{serverName}' has no {what} map.", nameof(values));
        }

        var pairs = new List<AcpFrames.NameValueFrame>(values.Count);
        foreach (var pair in values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
            {
                throw new ArgumentException($"MCP server '{serverName}' has a {what} entry with a blank name or a null value.", nameof(values));
            }

            pairs.Add(new AcpFrames.NameValueFrame { Name = pair.Key, Value = pair.Value });
        }

        return pairs.ToArray();
    }

    private static string? TextOf(JsonElement update) =>
        update.Deserialize(AcpJsonContext.Default.ChunkUpdate) is { Content: { Type: "text", Text: { } text } } ? text : null;

    private static ToolCallEvent ToolCall(JsonElement update)
    {
        var call = update.Deserialize(AcpJsonContext.Default.ToolCallPayload) ?? new AcpFrames.ToolCallPayload();
        return new ToolCallEvent(call.ToolCallId, call.Title ?? call.Kind ?? "tool", update.GetRawText());
    }

    private static ToolCallUpdateEvent ToolCallUpdate(JsonElement update)
    {
        var call = update.Deserialize(AcpJsonContext.Default.ToolCallPayload) ?? new AcpFrames.ToolCallPayload();
        return new ToolCallUpdateEvent(call.ToolCallId, call.Status ?? string.Empty, update.GetRawText());
    }

    private static PlanEvent Plan(JsonElement update)
    {
        var plan = update.Deserialize(AcpJsonContext.Default.PlanUpdate);
        return new PlanEvent(RenderPlan(plan?.Entries ?? []));
    }

    private static string Box(string status) => status switch
    {
        "completed" => "[x]",
        "in_progress" => "[~]",
        _ => "[ ]",
    };

    private static string Flag(bool value) => value ? "true" : "false";

    private static void AddPresence(Dictionary<string, string> map, string key, JsonElement? capability)
    {
        if (capability is not null)
        {
            map[key] = "true";
        }
    }
}
