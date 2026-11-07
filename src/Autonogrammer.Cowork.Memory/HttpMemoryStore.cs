using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Memory;

namespace Autonogrammer.Cowork.Memory;

/// <summary>
/// Desktop client for <c>/cowork/memory</c>. The bearer comes from the phase-67 token supplier on each call and is never stored.
/// A body that names an account is refused before send. Policy runs before the request leaves the machine.
/// </summary>
public sealed class HttpMemoryStore : IMemoryStore
{
    /// <summary>The collection route.</summary>
    public const string CollectionPath = "/cowork/memory";

    /// <summary>The recall route.</summary>
    public const string RecallPath = "/cowork/memory/recall";

    /// <summary>The reset route.</summary>
    public const string ResetPath = "/cowork/memory/reset";

    /// <summary>The export route.</summary>
    public const string ExportPath = "/cowork/memory/export";

    /// <summary>The settings route.</summary>
    public const string SettingsPath = "/cowork/memory/settings";

    private readonly HttpClient _http;
    private readonly Func<CancellationToken, ValueTask<string?>> _bearer;
    private readonly IMemoryNoticeSink? _notices;

    /// <summary>Initializes a new instance of the <see cref="HttpMemoryStore"/> class.</summary>
    /// <param name="http">The client. The base address is the account API. This type does not dispose it.</param>
    /// <param name="bearer">Supplies the account bearer, or null when the desktop is signed out.</param>
    /// <param name="notices">The notice sink, or null.</param>
    public HttpMemoryStore(HttpClient http, Func<CancellationToken, ValueTask<string?>> bearer, IMemoryNoticeSink? notices = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(bearer);
        _http = http;
        _bearer = bearer;
        _notices = notices;
    }

    /// <summary>Returns the path of one entry.</summary>
    /// <param name="id">The entry id.</param>
    /// <returns>The path.</returns>
    public static string EntryPath(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return CollectionPath + "/" + Uri.EscapeDataString(id);
    }

    /// <inheritdoc />
    public async Task<MemorySettings> GetSettingsAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var (ok, _, body) = await SendAsync(HttpMethod.Get, SettingsPath, null, cancellationToken).ConfigureAwait(false);
        if (!ok || body is null)
        {
            return new MemorySettings(false, false, false);
        }

        var record = JsonSerializer.Deserialize(body, MemoryJsonContext.Default.MemorySettingsRecord);
        return record is null ? new MemorySettings(false, false, false) : new MemorySettings(record.Enabled, record.Paused, record.IncludeSensitive);
    }

    /// <inheritdoc />
    public async Task SetSettingsAsync(string accountId, MemorySettings settings, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(settings);
        var json = JsonSerializer.Serialize(new MemorySettingsRecord { Enabled = settings.Enabled, Paused = settings.Paused, IncludeSensitive = settings.IncludeSensitive }, MemoryJsonContext.Default.MemorySettingsRecord);
        _ = await SendAsync(HttpMethod.Put, SettingsPath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> SaveAsync(MemoryScope scope, MemoryDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        var settings = await GetSettingsAsync(scope.AccountId, cancellationToken).ConfigureAwait(false);
        var decision = MemoryContentPolicy.Evaluate(draft.Topic, draft.Content, draft.Category, settings.IncludeSensitive);
        if (decision.Verdict != MemoryPolicyVerdict.Allowed)
        {
            var refusedCode = decision.Verdict == MemoryPolicyVerdict.RefusedNeverSave ? MemoryCodes.MemoryRefusedNeverSave : MemoryCodes.MemoryRefusedSensitive;
            _notices?.Report(new MemoryNotice(refusedCode, decision.Sentence!));
            return new MemoryMutation(false, refusedCode, decision.Sentence, null);
        }

        var limit = MemoryContentPolicy.LimitSentence(draft.Topic, draft.Content);
        if (limit is not null)
        {
            _notices?.Report(new MemoryNotice(MemoryCodes.MemoryCapReached, limit));
            return new MemoryMutation(false, MemoryCodes.MemoryCapReached, limit, null);
        }

        var json = JsonSerializer.Serialize(
            new MemorySaveRequest
            {
                Topic = draft.Topic,
                Content = draft.Content,
                Category = draft.Category,
                ProjectId = scope.ProjectId,
                SourceSession = scope.SessionId,
                UseAccountMemory = scope.UseAccountMemory,
            },
            MemoryJsonContext.Default.MemorySaveRequest);
        var (ok, code, body) = await SendAsync(HttpMethod.Post, CollectionPath, json, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            return Failure(code, body);
        }

        var record = body is null ? null : JsonSerializer.Deserialize(body, MemoryJsonContext.Default.MemoryEntryRecord);
        return new MemoryMutation(true, MemoryCodes.Ok, null, record is null ? null : FromRecord(scope.AccountId, record));
    }

    /// <inheritdoc />
    public async Task<MemoryRecall> RecallAsync(MemoryScope scope, string? query, int k, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (query is not null && query.Length > MemoryOptions.MaxQueryChars)
        {
            return new MemoryRecall(false, MemoryCodes.MemoryQueryTooLong, "The query is limited to " + MemoryOptions.MaxQueryChars.ToString(System.Globalization.CultureInfo.InvariantCulture) + " characters.", []);
        }

        var json = JsonSerializer.Serialize(new MemoryRecallRequest { Query = query, K = k, ProjectId = scope.ProjectId, UseAccountMemory = scope.UseAccountMemory }, MemoryJsonContext.Default.MemoryRecallRequest);
        var (ok, code, body) = await SendAsync(HttpMethod.Post, RecallPath, json, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            var failure = Failure(code, body);
            return new MemoryRecall(false, failure.Code, failure.Sentence, []);
        }

        var response = body is null ? null : JsonSerializer.Deserialize(body, MemoryJsonContext.Default.MemoryRecallResponse);
        var entries = response?.Entries.Select(record => FromRecord(scope.AccountId, record)).ToArray() ?? [];
        return new MemoryRecall(true, MemoryCodes.Ok, null, entries);
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> DeleteAsync(MemoryScope scope, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var path = EntryPath(id) + "?projectId=" + Uri.EscapeDataString(scope.ProjectId ?? string.Empty) + "&useAccountMemory=" + (scope.UseAccountMemory ? "true" : "false");
        var (ok, code, body) = await SendAsync(HttpMethod.Delete, path, null, cancellationToken).ConfigureAwait(false);
        return ok ? new MemoryMutation(true, MemoryCodes.Ok, null, null) : Failure(code, body);
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> ResetAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var (ok, code, body) = await SendAsync(HttpMethod.Post, ResetPath, null, cancellationToken).ConfigureAwait(false);
        return ok ? new MemoryMutation(true, MemoryCodes.Ok, null, null) : Failure(code, body);
    }

    /// <inheritdoc />
    public async Task<MemoryExport> ExportAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var (ok, code, body) = await SendAsync(HttpMethod.Get, ExportPath, null, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            var failure = Failure(code, body);
            return new MemoryExport(false, failure.Code, failure.Sentence, null);
        }

        return new MemoryExport(true, MemoryCodes.Ok, null, body);
    }

    /// <summary>Returns whether a JSON body names an account or owner.</summary>
    /// <param name="json">The body.</param>
    /// <returns>True when the body names an account.</returns>
    public static bool BodyNamesAccount(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        return json.Contains("\"account\"", StringComparison.OrdinalIgnoreCase)
            || json.Contains("\"accountId\"", StringComparison.OrdinalIgnoreCase)
            || json.Contains("\"owner\"", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool Ok, string Code, string? Body)> SendAsync(HttpMethod method, string path, string? content, CancellationToken cancellationToken)
    {
        if (content is not null && BodyNamesAccount(content))
        {
            return (false, MemoryCodes.SignedOut, null);
        }

        var token = await _bearer(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, MemoryCodes.SignedOut, null);
        }

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (false, MemoryCodes.SignedOut, body);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, CodeOf(body), body);
            }

            return (true, MemoryCodes.Ok, body);
        }
        catch (HttpRequestException)
        {
            return (false, MemoryCodes.Offline, null);
        }
    }

    private MemoryMutation Failure(string code, string? body)
    {
        var parsed = ParseError(body);
        var sentence = parsed?.Sentence ?? "Memory could not be updated.";
        if (code is MemoryCodes.MemoryRefusedNeverSave or MemoryCodes.MemoryRefusedSensitive or MemoryCodes.MemoryCapReached or MemoryCodes.MemoryPaused)
        {
            _notices?.Report(new MemoryNotice(code, sentence));
        }

        return new MemoryMutation(false, code, sentence, null);
    }

    private static string CodeOf(string? body) => ParseError(body)?.Code ?? MemoryCodes.Offline;

    private static MemoryErrorRecord? ParseError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, MemoryJsonContext.Default.MemoryErrorRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MemoryEntry FromRecord(string accountId, MemoryEntryRecord record) => new()
    {
        Id = record.Id,
        AccountId = accountId,
        ProjectId = record.ProjectId,
        Topic = record.Topic,
        Content = record.Content,
        Category = record.Category,
        SourceSession = record.SourceSession,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
    };
}
