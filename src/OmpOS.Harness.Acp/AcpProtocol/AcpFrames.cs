using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.Acp.AcpProtocol;

/// <summary>
/// Wire DTOs for the ACP v1 JSON-RPC 2.0 subset this client speaks. The DTOs are nested so that one protocol surface
/// lives in one file; they are reached only through this type. Outbound frames are positional records; inbound DTOs
/// are tolerant classes so absent members take their defaults and unknown members are skipped.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "The ACP wire DTOs are grouped under one protocol type so the whole surface lives in one file; they are only reached through AcpFrames.")]
public static class AcpFrames
{
    /// <summary>The JSON-RPC version string carried by every frame.</summary>
    public const string JsonRpcVersion = "2.0";

    /// <summary>The ACP protocol major version this client speaks.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>A client-to-agent request. <c>jsonrpc</c> is emitted first so frames match the captured wire form.</summary>
    /// <typeparam name="T">The params type.</typeparam>
    /// <param name="Id">The request identifier.</param>
    /// <param name="Method">The ACP method name.</param>
    /// <param name="Params">The method parameters.</param>
    public sealed record Request<T>(long Id, string Method, T Params)
    {
        /// <summary>Gets the JSON-RPC version.</summary>
        [JsonPropertyOrder(-1)]
        public string Jsonrpc { get; } = JsonRpcVersion;
    }

    /// <summary>A notification: a request without an identifier, so no response is expected.</summary>
    /// <typeparam name="T">The params type.</typeparam>
    /// <param name="Method">The ACP method name.</param>
    /// <param name="Params">The notification parameters.</param>
    public sealed record Notification<T>(string Method, T Params)
    {
        /// <summary>Gets the JSON-RPC version.</summary>
        [JsonPropertyOrder(-1)]
        public string Jsonrpc { get; } = JsonRpcVersion;
    }

    /// <summary>A successful response to an agent-initiated request.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="Id">The identifier of the agent request being answered.</param>
    /// <param name="Result">The result payload.</param>
    public sealed record Response<T>(long Id, T Result)
    {
        /// <summary>Gets the JSON-RPC version.</summary>
        [JsonPropertyOrder(-1)]
        public string Jsonrpc { get; } = JsonRpcVersion;
    }

    /// <summary>An error response to an agent-initiated request.</summary>
    /// <param name="Id">The identifier of the agent request being answered.</param>
    /// <param name="Error">The JSON-RPC error.</param>
    public sealed record ErrorResponse(long Id, AcpError Error)
    {
        /// <summary>Gets the JSON-RPC version.</summary>
        [JsonPropertyOrder(-1)]
        public string Jsonrpc { get; } = JsonRpcVersion;
    }

    /// <summary>
    /// An error response to an agent-initiated request that carries a string identifier. JSON-RPC 2.0 allows
    /// string ids; this client correlates its own requests numerically but echoes a string id verbatim instead
    /// of faulting the session.
    /// </summary>
    /// <param name="Id">The raw identifier text of the agent request being answered.</param>
    /// <param name="Error">The JSON-RPC error.</param>
    public sealed record StringErrorResponse(string Id, AcpError Error)
    {
        /// <summary>Gets the JSON-RPC version.</summary>
        [JsonPropertyOrder(-1)]
        public string Jsonrpc { get; } = JsonRpcVersion;
    }

    /// <summary>Decoding envelope for every frame the agent sends. Exactly one of the shapes applies per frame.</summary>
    public sealed class Inbound
    {
        /// <summary>Gets the raw request identifier, when present.</summary>
        public JsonElement? Id { get; init; }

        /// <summary>Gets the method name, present on notifications and agent requests.</summary>
        public string? Method { get; init; }

        /// <summary>Gets the raw params, present on notifications and agent requests.</summary>
        public JsonElement? Params { get; init; }

        /// <summary>Gets the raw result, present on successful responses.</summary>
        public JsonElement? Result { get; init; }

        /// <summary>Gets the error, present on failed responses.</summary>
        public AcpError? Error { get; init; }
    }

    /// <summary>A JSON-RPC error object.</summary>
    public sealed class AcpError
    {
        /// <summary>Gets the JSON-RPC error code.</summary>
        public long Code { get; init; }

        /// <summary>Gets the human-readable message.</summary>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>File-system capability flags the client advertises. Both are <see langword="true"/> only when <c>FsRoot</c> is configured.</summary>
    /// <param name="ReadTextFile">Whether the client implements <c>fs/read_text_file</c>.</param>
    /// <param name="WriteTextFile">Whether the client implements <c>fs/write_text_file</c>.</param>
    public sealed record FsCapabilities(bool ReadTextFile, bool WriteTextFile);

    /// <summary>Capabilities the client advertises in <c>initialize</c>.</summary>
    /// <param name="Fs">File-system capabilities.</param>
    public sealed record ClientCapabilities(FsCapabilities Fs);

    /// <summary>Parameters of <c>initialize</c>.</summary>
    /// <param name="ProtocolVersion">The protocol version the client speaks.</param>
    /// <param name="ClientCapabilities">The capabilities the client advertises.</param>
    public sealed record InitializeParams(int ProtocolVersion, ClientCapabilities ClientCapabilities);

    /// <summary>Parameters of an agent-initiated <c>fs/read_text_file</c> request.</summary>
    public sealed class FsReadParams
    {
        /// <summary>Gets the session the request belongs to.</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets the requested path, resolved against the configured fs root.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>Gets the 1-based first line to return, when provided.</summary>
        public int? Line { get; init; }

        /// <summary>Gets the maximum number of lines to return, when provided.</summary>
        public int? Limit { get; init; }
    }

    /// <summary>Parameters of an agent-initiated <c>fs/write_text_file</c> request.</summary>
    public sealed class FsWriteParams
    {
        /// <summary>Gets the session the request belongs to.</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets the requested path, resolved against the configured fs root.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>Gets the content to write.</summary>
        public string? Content { get; init; }
    }

    /// <summary>Result of <c>fs/read_text_file</c>.</summary>
    /// <param name="Content">The file content honouring <c>line</c> and <c>limit</c> when provided.</param>
    public sealed record FsReadResult(string Content);

    /// <summary>Result of <c>fs/write_text_file</c>. Empty: success is the absence of an error.</summary>
    public sealed record FsWriteResult;

    /// <summary>Identity the agent reports about itself.</summary>
    public sealed class AgentInfo
    {
        /// <summary>Gets the agent name, for example <c>omp</c>.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the display title.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the agent version.</summary>
        public string? Version { get; init; }
    }

    /// <summary>An authentication method the agent accepts.</summary>
    public sealed class AuthMethod
    {
        /// <summary>Gets the method identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the display name.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the description.</summary>
        public string? Description { get; init; }
    }

    /// <summary>Parameters of <c>authenticate</c>.</summary>
    /// <param name="MethodId">The advertised authentication method id to use.</param>
    public sealed record AuthenticateParams(string MethodId);

    /// <summary>MCP transport capabilities the agent supports.</summary>
    public sealed class McpCapabilities
    {
        /// <summary>Gets a value indicating whether HTTP MCP servers are supported.</summary>
        public bool Http { get; init; }

        /// <summary>Gets a value indicating whether SSE MCP servers are supported.</summary>
        public bool Sse { get; init; }
    }

    /// <summary>Prompt content kinds the agent accepts.</summary>
    public sealed class PromptCapabilities
    {
        /// <summary>Gets a value indicating whether embedded context is accepted.</summary>
        public bool EmbeddedContext { get; init; }

        /// <summary>Gets a value indicating whether images are accepted.</summary>
        public bool Image { get; init; }
    }

    /// <summary>Session management capabilities. Presence of an object means the operation is supported.</summary>
    public sealed class SessionCapabilities
    {
        /// <summary>Gets the <c>list</c> capability object, when supported.</summary>
        public JsonElement? List { get; init; }

        /// <summary>Gets the <c>fork</c> capability object, when supported.</summary>
        public JsonElement? Fork { get; init; }

        /// <summary>Gets the <c>resume</c> capability object, when supported.</summary>
        public JsonElement? Resume { get; init; }

        /// <summary>Gets the <c>close</c> capability object, when supported.</summary>
        public JsonElement? Close { get; init; }
    }

    /// <summary>Capabilities the agent reports in <c>initialize</c>.</summary>
    public sealed class AgentCapabilities
    {
        /// <summary>Gets a value indicating whether <c>session/load</c> is supported.</summary>
        public bool LoadSession { get; init; }

        /// <summary>Gets the MCP transport capabilities, or null when the agent omitted the group.</summary>
        public McpCapabilities? McpCapabilities { get; init; }

        /// <summary>Gets the prompt content capabilities, or null when the agent omitted the group.</summary>
        public PromptCapabilities? PromptCapabilities { get; init; }

        /// <summary>Gets the session management capabilities, or null when the agent omitted the group.</summary>
        public SessionCapabilities? SessionCapabilities { get; init; }
    }

    /// <summary>Result of <c>initialize</c>.</summary>
    public sealed class InitializeResult
    {
        /// <summary>Gets the protocol version the agent selected.</summary>
        public int ProtocolVersion { get; init; }

        /// <summary>Gets the agent identity.</summary>
        public AgentInfo? AgentInfo { get; init; }

        /// <summary>Gets the authentication methods.</summary>
        public IReadOnlyList<AuthMethod>? AuthMethods { get; init; }

        /// <summary>Gets the agent capabilities.</summary>
        public AgentCapabilities AgentCapabilities { get; init; } = new();
    }

    /// <summary>Parameters of <c>session/new</c>.</summary>
    /// <param name="Cwd">The absolute working directory for the session.</param>
    /// <param name="McpServers">MCP servers to attach. An empty list serialises as <c>[]</c>.</param>
    public sealed record NewSessionParams(string Cwd, IReadOnlyList<McpServerFrame> McpServers);

    /// <summary>
    /// One MCP server entry of <c>session/new</c> or <c>session/load</c>. Stdio entries set <see cref="Command"/>,
    /// <see cref="Args"/> and <see cref="Env"/> and no <see cref="Type"/>; http and sse entries set <see cref="Type"/>,
    /// <see cref="Url"/> and <see cref="Headers"/>. Unset members are not written. Header and env values are secrets,
    /// so this type does not print its members.
    /// </summary>
    public sealed class McpServerFrame
    {
        /// <summary>Gets the transport type (<c>http</c> or <c>sse</c>); <see langword="null"/> for stdio entries.</summary>
        public string? Type { get; init; }

        /// <summary>Gets the server name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the executable of a stdio server.</summary>
        public string? Command { get; init; }

        /// <summary>Gets the arguments of a stdio server.</summary>
        public IReadOnlyList<string>? Args { get; init; }

        /// <summary>Gets the environment of a stdio server.</summary>
        public IReadOnlyList<NameValueFrame>? Env { get; init; }

        /// <summary>Gets the endpoint of an http or sse server.</summary>
        public Uri? Url { get; init; }

        /// <summary>Gets the request headers of an http or sse server.</summary>
        public IReadOnlyList<NameValueFrame>? Headers { get; init; }
    }

    /// <summary>A <c>{name, value}</c> pair used for MCP env entries and headers.</summary>
    public sealed class NameValueFrame
    {
        /// <summary>Gets the variable or header name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the secret value.</summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>Result of <c>session/new</c>. The agent's <c>configOptions</c> array is kept as an opaque element and read by the config option parser, which skips every kind it does not model.</summary>
    public sealed class NewSessionResult
    {
        /// <summary>Gets the session identifier.</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets the opaque <c>configOptions</c> array, when the agent sent one.</summary>
        public JsonElement? ConfigOptions { get; init; }
    }

    /// <summary>
    /// The result of <c>session/load</c> and of <c>session/set_config_option</c>: the agent's rebuilt <c>configOptions</c> array,
    /// kept as an opaque element and read by the config option parser. Every other member is skipped.
    /// </summary>
    public sealed class ConfigOptionsResult
    {
        /// <summary>Gets the opaque <c>configOptions</c> array, when the agent sent one.</summary>
        public JsonElement? ConfigOptions { get; init; }
    }

    /// <summary>Parameters of <c>session/set_config_option</c>.</summary>
    /// <param name="SessionId">The session whose option changes.</param>
    /// <param name="ConfigId">The option identifier, for example <c>model</c>.</param>
    /// <param name="Value">The option value to select.</param>
    public sealed record SetConfigOptionParams(string SessionId, string ConfigId, string Value);

    /// <summary>Parameters of <c>session/load</c>.</summary>
    /// <param name="SessionId">The session to resume.</param>
    /// <param name="Cwd">The absolute working directory for the session.</param>
    /// <param name="McpServers">MCP servers to attach. An empty list serialises as <c>[]</c>.</param>
    public sealed record LoadSessionParams(string SessionId, string Cwd, IReadOnlyList<McpServerFrame> McpServers);

    /// <summary>Parameters of <c>session/close</c>.</summary>
    /// <param name="SessionId">The session to close.</param>
    public sealed record CloseSessionParams(string SessionId);

    /// <summary>A prompt or message content block.</summary>
    public sealed class ContentBlock
    {
        /// <summary>Gets the block kind. Always <c>text</c> for blocks this client sends.</summary>
        public string Type { get; init; } = "text";

        /// <summary>Gets the text, for text blocks.</summary>
        public string? Text { get; init; }
    }

    /// <summary>Parameters of <c>session/prompt</c>.</summary>
    /// <param name="SessionId">The session the prompt belongs to.</param>
    /// <param name="Prompt">The prompt content blocks.</param>
    public sealed record PromptParams(string SessionId, IReadOnlyList<ContentBlock> Prompt);

    /// <summary>Result of <c>session/prompt</c>.</summary>
    public sealed class PromptResult
    {
        /// <summary>Gets why the turn stopped, for example <c>end_turn</c> or <c>cancelled</c>.</summary>
        public string StopReason { get; init; } = string.Empty;
    }

    /// <summary>Parameters of <c>session/cancel</c>.</summary>
    /// <param name="SessionId">The session whose turn is cancelled.</param>
    public sealed record CancelParams(string SessionId);

    /// <summary>Parameters of <c>$/cancel_request</c>.</summary>
    /// <param name="RequestId">The identifier of the request to cancel.</param>
    public sealed record CancelRequestParams(long RequestId);

    /// <summary>Parameters of <c>session/update</c>. The update is decoded later by its <c>sessionUpdate</c> discriminator.</summary>
    public sealed class SessionNotification
    {
        /// <summary>Gets the session the update belongs to.</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets the raw update object.</summary>
        public JsonElement Update { get; init; }
    }

    /// <summary>The discriminator of a session update.</summary>
    public sealed class SessionUpdateHeader
    {
        /// <summary>Gets the update kind, for example <c>agent_message_chunk</c>.</summary>
        public string SessionUpdate { get; init; } = string.Empty;
    }

    /// <summary>A content chunk update.</summary>
    public sealed class ChunkUpdate
    {
        /// <summary>Gets the chunk content.</summary>
        public ContentBlock? Content { get; init; }
    }

    /// <summary>A tool call start or progress update.</summary>
    public sealed class ToolCallPayload
    {
        /// <summary>Gets the tool call identifier.</summary>
        public string ToolCallId { get; init; } = string.Empty;

        /// <summary>Gets the display title.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the tool kind, for example <c>read</c> or <c>execute</c>.</summary>
        public string? Kind { get; init; }

        /// <summary>Gets the status.</summary>
        public string? Status { get; init; }
    }

    /// <summary>A plan update.</summary>
    public sealed class PlanUpdate
    {
        /// <summary>Gets the plan entries.</summary>
        public IReadOnlyList<PlanEntry>? Entries { get; init; }
    }

    /// <summary>One plan entry.</summary>
    public sealed class PlanEntry
    {
        /// <summary>Gets the entry text.</summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>Gets the entry status: <c>pending</c>, <c>in_progress</c> or <c>completed</c>.</summary>
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>Parameters of an agent-initiated <c>session/request_permission</c> request.</summary>
    public sealed class PermissionRequestParams
    {
        /// <summary>Gets the session the request belongs to.</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Gets the tool call that needs permission.</summary>
        public ToolCallRef? ToolCall { get; init; }

        /// <summary>Gets the options the agent offers.</summary>
        public IReadOnlyList<PermissionOption>? Options { get; init; }
    }

    /// <summary>The tool call referenced by a permission request.</summary>
    public sealed class ToolCallRef
    {
        /// <summary>Gets the tool call identifier.</summary>
        public string ToolCallId { get; init; } = string.Empty;

        /// <summary>Gets the display title.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the tool kind.</summary>
        public string? Kind { get; init; }
    }

    /// <summary>One option offered in a permission request.</summary>
    public sealed class PermissionOption
    {
        /// <summary>Gets the option identifier to echo back when selected.</summary>
        public string OptionId { get; init; } = string.Empty;

        /// <summary>Gets the display name.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the option kind: <c>allow_once</c>, <c>allow_always</c>, <c>reject_once</c> or <c>reject_always</c>.</summary>
        public string Kind { get; init; } = string.Empty;
    }

    /// <summary>The outcome of a permission request: <c>selected</c> with an option, or <c>cancelled</c>.</summary>
    /// <param name="Outcome">The outcome kind.</param>
    /// <param name="OptionId">The selected option, for <c>selected</c> outcomes.</param>
    public sealed record PermissionOutcome(string Outcome, string? OptionId);

    /// <summary>Result of a permission request.</summary>
    /// <param name="Outcome">The outcome.</param>
    public sealed record PermissionResponse(PermissionOutcome Outcome);
}
