using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="IOmpApiClient"/> over one <see cref="HttpClient"/> whose base address is the API origin. JSON goes through the source-generated
/// contexts of <c>Autonogrammer.Contracts</c> (strict, reflection free). The per-session event stream is <see cref="SseEventStream"/>.
/// </summary>
internal sealed class OmpApiClient : IOmpApiClient
{
    private readonly HttpClient _http;
    private readonly SseEventStream _events;

    /// <summary>Initializes a new instance of the <see cref="OmpApiClient"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    /// <param name="streamOptions">The reconnect policy of the session stream, or null for the defaults.</param>
    internal OmpApiClient(HttpClient http, SseStreamOptions? streamOptions = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _events = new SseEventStream(http, streamOptions);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionCatalogEntry>> ListSessionsAsync(CancellationToken cancellationToken) =>
        ListAsync("sessions", CatalogJsonContext.Default.SessionCatalogEntryArray, cancellationToken);

    /// <inheritdoc />
    public async Task<Session> CreateSessionAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            Route("sessions"),
            new CreateSessionRequest(),
            WebJsonContext.Default.CreateSessionRequest,
            cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, AutonogrammerJsonContext.Default.Session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Session> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(Route(SessionPath(sessionId)), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, AutonogrammerJsonContext.Default.Session, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubmitPromptAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        using var response = await _http.PostAsJsonAsync(
            Route(SessionPath(sessionId, "prompts")),
            new SubmitPromptRequest { Prompt = prompt },
            WebJsonContext.Default.SubmitPromptRequest,
            cancellationToken).ConfigureAwait(false);
        await ApiProblems.ThrowIfNotSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SteerResponse> SteerAsync(string sessionId, string text, SteerMode mode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        using var response = await _http.PostAsJsonAsync(
            Route(SessionPath(sessionId, "steer")),
            new SteerRequest { Text = text, Mode = mode },
            SessionControlsJsonContext.Default.SteerRequest,
            cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, SessionControlsJsonContext.Default.SteerResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PendingToolApprovalsResponse> GetToolApprovalsAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "tool-approvals"), SessionControlsJsonContext.Default.PendingToolApprovalsResponse, cancellationToken);

    /// <inheritdoc />
    public async Task DecideToolApprovalAsync(string sessionId, string requestId, bool allow, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        using var response = await _http.PostAsJsonAsync(
            Route(SessionPath(sessionId, "tool-approvals", Uri.EscapeDataString(requestId))),
            new ToolDecisionRequest { Decision = allow ? "allow" : "deny" },
            WebJsonContext.Default.ToolDecisionRequest,
            cancellationToken).ConfigureAwait(false);
        await ApiProblems.ThrowIfNotSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<StreamEvent> StreamSessionAsync(string sessionId, long? sinceSeq, CancellationToken cancellationToken) =>
        _events.StreamSessionAsync(sessionId, sinceSeq, cancellationToken);

    /// <inheritdoc />
    public Task<SessionStagesResponse> GetStagesAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "stages"), CatalogJsonContext.Default.SessionStagesResponse, cancellationToken);

    /// <inheritdoc />
    public Task<SessionContextResponse> GetContextAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "context"), CatalogJsonContext.Default.SessionContextResponse, cancellationToken);

    /// <inheritdoc />
    public Task<SessionConfigOptionsResponse> GetConfigOptionsAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "config-options"), SessionControlsJsonContext.Default.SessionConfigOptionsResponse, cancellationToken);

    /// <inheritdoc />
    public async Task<SessionConfigOptionsResponse> SetConfigOptionAsync(
        string sessionId,
        string optionId,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        using var response = await _http.PutAsJsonAsync(
            Route(SessionPath(sessionId, "config-options", Uri.EscapeDataString(optionId))),
            new SetConfigOptionRequest { Value = value },
            SessionControlsJsonContext.Default.SetConfigOptionRequest,
            cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, SessionControlsJsonContext.Default.SessionConfigOptionsResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<SessionCommandsResponse> GetCommandsAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "commands"), SessionControlsJsonContext.Default.SessionCommandsResponse, cancellationToken);

    /// <inheritdoc />
    public Task<SessionChangesResponse> GetChangesAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "changes"), SessionControlsJsonContext.Default.SessionChangesResponse, cancellationToken);

    /// <inheritdoc />
    public async Task<ChangeDecisionResponse> DecideChangeAsync(string sessionId, string path, bool accept, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var response = await _http.PostAsync(
            Route(SessionPath(sessionId, "changes", Uri.EscapeDataString(path), accept ? "accept" : "reject")),
            null,
            cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, SessionControlsJsonContext.Default.ChangeDecisionResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<BoardResponse> GetBoardAsync(CancellationToken cancellationToken) =>
        GetAsync("board", CatalogJsonContext.Default.BoardResponse, cancellationToken);

    /// <inheritdoc />
    public Task<CardDetailsResponse> GetCardDetailsAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync(SessionPath(sessionId, "card-details"), CatalogJsonContext.Default.CardDetailsResponse, cancellationToken);

    /// <inheritdoc />
    public Task<SessionLifecycleResponse> CloseSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        PostLifecycleAsync(sessionId, "close", cancellationToken);

    /// <inheritdoc />
    public Task<SessionLifecycleResponse> ReopenSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        PostLifecycleAsync(sessionId, "reopen", cancellationToken);

    /// <inheritdoc />
    public Task<SessionLifecycleResponse> ResumeSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        PostLifecycleAsync(sessionId, "resume", cancellationToken);

    /// <inheritdoc />
    public Task<WorkspaceInfo> GetWorkspaceAsync(CancellationToken cancellationToken) =>
        GetAsync("workspace", CatalogJsonContext.Default.WorkspaceInfo, cancellationToken);

    /// <inheritdoc />
    public Task<SourceInfo> GetSourceAsync(CancellationToken cancellationToken) =>
        GetAsync("source", CatalogJsonContext.Default.SourceInfo, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(Route("health/ready"), cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private static Uri Route(string path) => new(path, UriKind.Relative);

    private static string SessionPath(string sessionId, params string[] tail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return tail.Length == 0
            ? $"sessions/{Uri.EscapeDataString(sessionId)}"
            : $"sessions/{Uri.EscapeDataString(sessionId)}/{string.Join('/', tail)}";
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await ApiProblems.ThrowIfNotSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        T? value;
        try
        {
            value = await BoundedContent.ReadJsonAsync(response.Content, typeInfo, BoundedContent.MaxSuccessBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException fault)
        {
            throw new ApiClientException("The API returned a body that is not the documented shape.", fault);
        }

        return value ?? throw new ApiClientException("The API returned an empty body.");
    }

    private async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(Route(path), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> ListAsync<T>(string path, JsonTypeInfo<T[]> typeInfo, CancellationToken cancellationToken)
    {
        T[] items = await GetAsync(path, typeInfo, cancellationToken).ConfigureAwait(false);
        return items;
    }

    private async Task<SessionLifecycleResponse> PostLifecycleAsync(string sessionId, string action, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(Route(SessionPath(sessionId, action)), null, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, CatalogJsonContext.Default.SessionLifecycleResponse, cancellationToken).ConfigureAwait(false);
    }
}
