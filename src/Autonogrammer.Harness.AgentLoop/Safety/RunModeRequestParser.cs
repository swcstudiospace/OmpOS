using System.Text.Json;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Reads a permission request that omp sent over ACP into a <see cref="RunModeRequest"/>. The ACP tool kind picks the kind of call
/// (<c>execute</c> is a shell command, <c>read</c> and <c>search</c> read files, <c>edit</c>, <c>delete</c> and <c>move</c> write them, <c>fetch</c> fetches), and a tool
/// named like an MCP tool is an MCP call. The command, files and address come from the call's <c>rawInput</c> and <c>locations</c>; their field names are
/// UNVERIFIED against omp (<c>omp acp</c> with a fake key and no model turn, reading a permission request), so several spellings are accepted and a call without
/// the field its kind needs becomes a request that asks.
/// </summary>
public static class RunModeRequestParser
{
    private static readonly string[] CommandKeys = ["command", "cmd"];
    private static readonly string[] WorkingDirectoryKeys = ["cwd", "workdir", "workingDirectory"];
    private static readonly string[] PathKeys = ["path", "file_path", "filePath", "file", "paths", "files", "source", "src", "destination", "dest", "target"];
    private static readonly string[] AddressKeys = ["url", "uri"];
    private static readonly string[] NameKeys = ["toolName", "tool_name"];

    /// <summary>Reads a request. It never throws: a payload it cannot read becomes a call of an unknown kind, which asks.</summary>
    /// <param name="request">The permission request.</param>
    /// <returns>The request for the decision engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static RunModeRequest Parse(PermissionRequestEvent request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var document = TryParse(request.PayloadJson);
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("toolCall", out var call)
            || call.ValueKind != JsonValueKind.Object)
        {
            return new RunModeRequest(RunModeToolKind.Other, request.Name);
        }

        var name = Strings(call, NameKeys).FirstOrDefault() ?? Strings(call, ["title"]).FirstOrDefault() ?? request.Name;
        var input = call.TryGetProperty("rawInput", out var raw) && raw.ValueKind == JsonValueKind.Object ? raw : default;
        var workingDirectory = input.ValueKind == JsonValueKind.Object ? Strings(input, WorkingDirectoryKeys).FirstOrDefault() : null;
        if (McpToolName.TryParse(name, out var server, out var tool))
        {
            return new RunModeRequest(RunModeToolKind.Mcp, name, McpServer: server, McpTool: tool);
        }

        var kind = Strings(call, ["kind"]).FirstOrDefault() ?? string.Empty;
        switch (kind)
        {
            case "execute":
                var command = input.ValueKind == JsonValueKind.Object ? Command(input) : null;
                return new RunModeRequest(RunModeToolKind.Shell, name, Command: command, WorkingDirectory: workingDirectory);
            case "read" or "search":
                return new RunModeRequest(RunModeToolKind.Read, name, Paths: Paths(call, input), WorkingDirectory: workingDirectory);
            case "edit" or "delete" or "move":
                return new RunModeRequest(RunModeToolKind.Write, name, Paths: Paths(call, input), WorkingDirectory: workingDirectory);
            case "fetch":
                return new RunModeRequest(RunModeToolKind.Fetch, name, FetchAddress: input.ValueKind == JsonValueKind.Object ? Strings(input, AddressKeys).FirstOrDefault() : null);
            default:
                return new RunModeRequest(RunModeToolKind.Other, name);
        }
    }

    /// <summary>Returns the ACP tool kind of a permission request (<c>execute</c>, <c>read</c> and so on), or an empty string when the request names none. A head uses it to ask its <c>AcpApprovalPolicy</c> whether the request is admitted.</summary>
    /// <param name="request">The permission request.</param>
    /// <returns>The kind, or an empty string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static string AcpKind(PermissionRequestEvent request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var document = TryParse(request.PayloadJson);
        return document is not null
            && document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("toolCall", out var call)
            && call.ValueKind == JsonValueKind.Object
                ? Strings(call, ["kind"]).FirstOrDefault() ?? string.Empty
                : string.Empty;
    }

    private static JsonDocument? TryParse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Command(JsonElement input)
    {
        foreach (var key in CommandKeys)
        {
            if (!input.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var words = value.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString() ?? string.Empty).ToList();
                return words.Count == 0 ? null : string.Join(' ', words);
            }
        }

        return null;
    }

    private static List<string> Paths(JsonElement call, JsonElement input)
    {
        var paths = new List<string>();
        if (call.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array)
        {
            foreach (var location in locations.EnumerateArray())
            {
                if (location.ValueKind == JsonValueKind.Object)
                {
                    paths.AddRange(Strings(location, ["path"]));
                }
            }
        }

        if (input.ValueKind == JsonValueKind.Object)
        {
            paths.AddRange(Strings(input, PathKeys));
        }

        return [.. paths.Where(static path => path.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> Strings(JsonElement parent, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!parent.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
            {
                yield return text;
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } entry)
                    {
                        yield return entry;
                    }
                }
            }
        }
    }
}
