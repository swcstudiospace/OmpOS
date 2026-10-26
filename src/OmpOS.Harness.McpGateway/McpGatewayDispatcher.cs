using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using OmpOS.Harness.Mcp;

namespace OmpOS.Harness.McpGateway;

/// <summary>
/// The JSON-RPC 2.0 half of the gateway: parses one request body and answers <c>initialize</c>, <c>ping</c>,
/// <c>tools/list</c> and <c>tools/call</c>; every other method is a JSON-RPC <c>-32601</c> error. A call is matched against the
/// tools the registry grants to the calling session, and a tool outside that set is refused here, before the invoker, the
/// approval gate or any upstream server is reached.
/// </summary>
internal sealed class McpGatewayDispatcher
{
    private const string LatestProtocolVersion = "2025-11-25";
    private const string ServerName = "ompos-mcp-gateway";
    private const string ServerVersion = "1.0.0";
    private const string NotApprovedText = "The tool call was not approved.";
    private const string UpstreamFailedText = "The MCP server could not complete the call.";

    private static readonly string[] SupportedProtocolVersions = [LatestProtocolVersion, "2025-06-18", "2025-03-26"];

    private readonly McpToolRegistry _registry;
    private readonly McpToolInvoker _invoker;
    private readonly McpGatewaySessions _sessions;

    /// <summary>Creates a dispatcher.</summary>
    /// <param name="registry">The registry whose per-session enablement defines the grants. It must be the registry the invoker uses.</param>
    /// <param name="invoker">The only route to an upstream server.</param>
    /// <param name="sessions">The session tables shared with the HTTP layer.</param>
    public McpGatewayDispatcher(McpToolRegistry registry, McpToolInvoker invoker, McpGatewaySessions sessions)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(sessions);
        _registry = registry;
        _invoker = invoker;
        _sessions = sessions;
    }

    /// <summary>Answers one <c>POST</c> body.</summary>
    /// <param name="registrySessionId">The registry session named by the request path.</param>
    /// <param name="transportSessionId">The <c>Mcp-Session-Id</c> header, or <c>null</c> when absent.</param>
    /// <param name="body">The raw request body.</param>
    /// <param name="cancellationToken">Cancels an approval or an upstream call.</param>
    /// <returns>The reply to send.</returns>
    public async ValueTask<McpGatewayReply> HandlePostAsync(
        string registrySessionId,
        string? transportSessionId,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        JsonDocument? document = null;
        try
        {
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return Json(400, McpJsonRpc.Error(null, McpJsonRpc.ParseError, "Parse error."));
            }

            return await HandleMessageAsync(document.RootElement, registrySessionId, transportSessionId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            document?.Dispose();
        }
    }

    private static McpGatewayReply Json(int statusCode, ReadOnlyMemory<byte> body) => new(statusCode, body, null);

    private static McpGatewayReply Accepted() => new(202, ReadOnlyMemory<byte>.Empty, null);

    private static McpGatewayReply Failure(JsonElement? id, int code, string message) =>
        Json(200, McpJsonRpc.Error(id, code, message));

    private static McpGatewayReply Invalid(JsonElement? id, string message) =>
        Json(400, McpJsonRpc.Error(id, McpJsonRpc.InvalidRequest, message));

    private static string? RequestedProtocolVersion(JsonElement root) =>
        root.TryGetProperty("params", out var parameters)
        && parameters.ValueKind == JsonValueKind.Object
        && parameters.TryGetProperty("protocolVersion", out var version)
        && version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;

    private static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    private static void WriteEmptyObject(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private static McpGatewayReply ToolResult(JsonElement id, CallToolResult result)
    {
        var typeInfo = McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(CallToolResult));
        var serialized = JsonSerializer.Serialize(result, typeInfo);
        return Json(200, McpJsonRpc.Result(id, writer => writer.WriteRawValue(serialized)));
    }

    private static void WriteTool(Utf8JsonWriter writer, McpToolDescriptor tool)
    {
        writer.WriteStartObject();
        writer.WriteString("name", McpGatewayToolNames.Compose(tool.Id));
        if (tool.Description is not null)
        {
            writer.WriteString("description", tool.Description);
        }

        writer.WritePropertyName("inputSchema");
        if (tool.InputSchema.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteEndObject();
        }
        else
        {
            tool.InputSchema.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private async ValueTask<McpGatewayReply> HandleMessageAsync(
        JsonElement root,
        string registrySessionId,
        string? transportSessionId,
        CancellationToken cancellationToken)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid(null, "A single JSON-RPC message object is required.");
        }

        JsonElement? id = null;
        if (root.TryGetProperty("id", out var idElement))
        {
            if (idElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            {
                return Invalid(null, "The request id must be a string or a number.");
            }

            id = idElement;
        }

        if (!root.TryGetProperty("jsonrpc", out var version) || !version.ValueEquals("2.0"))
        {
            return Invalid(id, "The message must declare jsonrpc 2.0.");
        }

        if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
        {
            var isResponse = id is not null && (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _));
            return isResponse ? Accepted() : Invalid(id, "The message has no method.");
        }

        var method = methodElement.GetString() ?? string.Empty;
        if (string.Equals(method, "initialize", StringComparison.Ordinal))
        {
            return id is { } initializeId
                ? Initialize(initializeId, root, registrySessionId)
                : Invalid(null, "initialize must be a request with an id.");
        }

        var refused = RequireTransportSession(id, registrySessionId, transportSessionId);
        if (refused is not null)
        {
            return refused;
        }

        if (id is not { } requestId)
        {
            return Accepted();
        }

        return method switch
        {
            "ping" => Json(200, McpJsonRpc.Result(requestId, static writer => WriteEmptyObject(writer))),
            "tools/list" => ListTools(requestId, registrySessionId),
            "tools/call" => await CallToolAsync(requestId, root, registrySessionId, cancellationToken).ConfigureAwait(false),
            _ => Failure(requestId, McpJsonRpc.MethodNotFound, "Method not found."),
        };
    }

    private McpGatewayReply? RequireTransportSession(JsonElement? id, string registrySessionId, string? transportSessionId)
    {
        if (string.IsNullOrEmpty(transportSessionId))
        {
            return Invalid(id, "The Mcp-Session-Id header is required.");
        }

        return _sessions.IsBound(transportSessionId, registrySessionId)
            ? null
            : Json(404, McpJsonRpc.Error(id, McpJsonRpc.SessionNotFound, "Session not found."));
    }

    private McpGatewayReply Initialize(JsonElement id, JsonElement root, string registrySessionId)
    {
        var requested = RequestedProtocolVersion(root);
        if (requested is null)
        {
            return Failure(id, McpJsonRpc.InvalidParams, "initialize requires a protocolVersion.");
        }

        var negotiated = Array.Exists(SupportedProtocolVersions, version => string.Equals(version, requested, StringComparison.Ordinal))
            ? requested
            : LatestProtocolVersion;
        var transportSessionId = _sessions.BeginTransport(registrySessionId);
        var body = McpJsonRpc.Result(id, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("protocolVersion", negotiated);
            writer.WriteStartObject("capabilities");
            writer.WriteStartObject("tools");
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject("serverInfo");
            writer.WriteString("name", ServerName);
            writer.WriteString("version", ServerVersion);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        return new McpGatewayReply(200, body, transportSessionId);
    }

    private McpGatewayReply ListTools(JsonElement id, string registrySessionId)
    {
        var granted = _registry.GetOfferedTools(registrySessionId);
        return Json(200, McpJsonRpc.Result(id, writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("tools");
            foreach (var tool in granted)
            {
                WriteTool(writer, tool);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }));
    }

    private async ValueTask<McpGatewayReply> CallToolAsync(
        JsonElement id,
        JsonElement root,
        string registrySessionId,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String)
        {
            return Failure(id, McpJsonRpc.InvalidParams, "A tool name is required.");
        }

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters.TryGetProperty("arguments", out var argumentsElement) && argumentsElement.ValueKind != JsonValueKind.Null)
        {
            if (argumentsElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(id, McpJsonRpc.InvalidParams, "Tool arguments must be an object.");
            }

            foreach (var property in argumentsElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.Clone();
            }
        }

        var name = nameElement.GetString();
        var granted = _registry
            .GetOfferedTools(registrySessionId)
            .FirstOrDefault(tool => string.Equals(McpGatewayToolNames.Compose(tool.Id), name, StringComparison.Ordinal));
        if (granted is null)
        {
            return Failure(id, McpJsonRpc.InvalidParams, "Unknown tool.");
        }

        try
        {
            var result = await _invoker.InvokeAsync(registrySessionId, granted.Id, arguments, cancellationToken).ConfigureAwait(false);
            return ToolResult(id, result);
        }
        catch (McpToolNotOfferedException)
        {
            return Failure(id, McpJsonRpc.InvalidParams, "Unknown tool.");
        }
        catch (McpToolDeniedException)
        {
            return ToolResult(id, ErrorResult(NotApprovedText));
        }
        catch (Exception exception) when (exception is McpException or HttpRequestException or IOException or ObjectDisposedException)
        {
            return ToolResult(id, ErrorResult(UpstreamFailedText));
        }
    }
}
