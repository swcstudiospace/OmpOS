#pragma warning disable MA0048 // MemorySearch shares this file so the plan's file list stays closed.
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Memory;

namespace Autonogrammer.Cowork.Memory;

/// <summary>A process-local test store. Keyed replay is not durable or shared across instances.</summary>
public sealed class InMemoryMemoryStore : IMemoryStore, IMemoryIdempotentStore
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryNoticeSink? _notices;
    private readonly List<MemoryEntry> _entries = [];
    private readonly Dictionary<string, MemorySettings> _settings = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Account, string Key), (byte[] Fingerprint, string EntryId, DateTimeOffset Expires)> _claims = new();

    /// <summary>Initializes a new instance of the <see cref="InMemoryMemoryStore"/> class.</summary>
    /// <param name="time">The clock. The default is <see cref="TimeProvider.System"/>.</param>
    /// <param name="notices">The notice sink, or null.</param>
    public InMemoryMemoryStore(TimeProvider? time = null, IMemoryNoticeSink? notices = null)
    {
        _timeProvider = time ?? TimeProvider.System;
        _notices = notices;
    }

    /// <inheritdoc />
    public Task<MemorySettings> GetSettingsAsync(string accountId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        lock (_gate)
        {
            return Task.FromResult(Settings(accountId));
        }
    }

    /// <inheritdoc />
    public Task SetSettingsAsync(string accountId, MemorySettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            _settings[accountId] = settings;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<MemoryMutation> SaveAsync(MemoryScope scope, MemoryDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate)
        {
            return Task.FromResult(SaveCore(scope, draft));
        }
    }

    private MemoryMutation SaveCore(MemoryScope scope, MemoryDraft draft)
    {
        var refused = RefuseWrite(scope, draft);
        if (refused is not null)
        {
            return refused;
        }

        var now = _timeProvider.GetUtcNow();
        var entry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString("n"),
            AccountId = scope.AccountId,
            ProjectId = scope.ProjectId,
            Topic = draft.Topic,
            Content = draft.Content,
            Category = draft.Category,
            SourceSession = scope.SessionId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _entries.Add(entry);
        return new MemoryMutation(true, MemoryCodes.Ok, null, entry);
    }

    /// <inheritdoc />
    public Task<MemoryIdempotentResult> SaveIdempotentAsync(MemoryScope scope, MemoryDraft draft, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.AccountId, nameof(scope));
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is < 1 or > 128 || key.Any(static c => c < '!' || c > '~'))
        {
            throw new ArgumentException("The key must contain 1 to 128 visible ASCII characters.", nameof(key));
        }

        lock (_gate)
        {
            if (Settings(scope.AccountId).Paused)
            {
                return Task.FromResult(new MemoryIdempotentResult(MemoryIdempotentStatus.Refused, PausedWrite()));
            }

            var fingerprint = Fingerprint(scope, draft);
            var claimKey = (scope.AccountId, key);
            var prior = FindClaim(scope.AccountId, key, fingerprint);
            if (prior is not null)
            {
                return Task.FromResult(prior);
            }

            var mutation = SaveCore(scope, draft);
            if (!mutation.Succeeded || mutation.Entry is null)
            {
                return Task.FromResult(new MemoryIdempotentResult(MemoryIdempotentStatus.Refused, mutation));
            }

            _claims[claimKey] = (fingerprint, mutation.Entry.Id, _timeProvider.GetUtcNow().AddHours(24));
            return Task.FromResult(new MemoryIdempotentResult(MemoryIdempotentStatus.Created, mutation));
        }
    }

    private MemoryIdempotentResult? FindClaim(string accountId, string key, byte[] fingerprint)
    {
        var claimKey = (accountId, key);
        if (!_claims.TryGetValue(claimKey, out var claim))
        {
            return null;
        }

        if (claim.Expires <= _timeProvider.GetUtcNow())
        {
            _claims.Remove(claimKey);
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(claim.Fingerprint, fingerprint))
        {
            return new(MemoryIdempotentStatus.Conflict, null);
        }

        var entry = _entries.Find(item => string.Equals(item.AccountId, accountId, StringComparison.Ordinal) && string.Equals(item.Id, claim.EntryId, StringComparison.Ordinal));
        return entry is null
            ? new(MemoryIdempotentStatus.Gone, null)
            : new(MemoryIdempotentStatus.Replayed, new MemoryMutation(true, MemoryCodes.Ok, null, entry));
    }

    private static byte[] Fingerprint(MemoryScope scope, MemoryDraft draft)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var value in new[] { draft.Topic, draft.Content, draft.Category, scope.ProjectId, scope.UseAccountMemory ? "1" : "0", scope.SessionId })
        {
            var bytes = value is null ? null : Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes?.Length ?? -1);
            hash.AppendData(length);
            if (bytes is not null) hash.AppendData(bytes);
        }

        return hash.GetHashAndReset();
    }

    /// <inheritdoc />
    public Task<MemoryRecall> RecallAsync(MemoryScope scope, string? query, int k, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);
        lock (_gate)
        {
            if (Settings(scope.AccountId).Paused)
            {
                return Task.FromResult(PausedRecall());
            }

            if (query is not null && query.Length > MemoryOptions.MaxQueryChars)
            {
                return Task.FromResult(new MemoryRecall(false, MemoryCodes.MemoryQueryTooLong, "The query is limited to " + MemoryOptions.MaxQueryChars.ToString(CultureInfo.InvariantCulture) + " characters.", []));
            }

            var take = k < 1 ? 1 : k;
            var matched = _entries.Where(entry => MemorySearch.InScope(entry, scope) && MemorySearch.Matches(entry, query)).OrderByDescending(static entry => entry.UpdatedAt).Take(take).ToArray();
            return Task.FromResult(new MemoryRecall(true, MemoryCodes.Ok, null, matched));
        }
    }

    /// <inheritdoc />
    public Task<MemoryMutation> DeleteAsync(MemoryScope scope, string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            if (Settings(scope.AccountId).Paused)
            {
                return Task.FromResult(PausedWrite());
            }

            var index = _entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.Ordinal) && MemorySearch.InScope(entry, scope));
            if (index < 0)
            {
                return Task.FromResult(new MemoryMutation(false, MemoryCodes.MemoryNotFound, "That memory entry was not found.", null));
            }

            _entries.RemoveAt(index);
            return Task.FromResult(new MemoryMutation(true, MemoryCodes.Ok, null, null));
        }
    }

    /// <inheritdoc />
    public Task<MemoryMutation> ResetAsync(string accountId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        lock (_gate)
        {
            _entries.RemoveAll(entry => string.Equals(entry.AccountId, accountId, StringComparison.Ordinal));
            return Task.FromResult(new MemoryMutation(true, MemoryCodes.Ok, null, null));
        }
    }

    /// <inheritdoc />
    public Task<MemoryExport> ExportAsync(string accountId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        lock (_gate)
        {
            var records = _entries.Where(entry => string.Equals(entry.AccountId, accountId, StringComparison.Ordinal)).Select(MemorySearch.ToRecord).ToList();
            var json = JsonSerializer.Serialize(new MemoryExportDocument { Entries = records }, MemoryJsonContext.Default.MemoryExportDocument);
            return Task.FromResult(new MemoryExport(true, MemoryCodes.Ok, null, json));
        }
    }

    private MemoryMutation? RefuseWrite(MemoryScope scope, MemoryDraft draft)
    {
        if (Settings(scope.AccountId).Paused)
        {
            var paused = PausedWrite();
            _notices?.Report(new MemoryNotice(paused.Code, paused.Sentence!));
            return paused;
        }

        var limit = MemoryContentPolicy.LimitSentence(draft.Topic, draft.Content);
        if (limit is not null)
        {
            _notices?.Report(new MemoryNotice(MemoryCodes.MemoryCapReached, limit));
            return new MemoryMutation(false, MemoryCodes.MemoryCapReached, limit, null);
        }

        var since = _timeProvider.GetUtcNow().AddHours(-1);
        var saves = _entries.Count(entry => string.Equals(entry.AccountId, scope.AccountId, StringComparison.Ordinal) && string.Equals(entry.SourceSession, scope.SessionId, StringComparison.Ordinal) && entry.CreatedAt >= since);
        if (saves >= MemoryOptions.MaxSavesPerSessionPerHour)
        {
            const string sentence = "This was not saved. This session has used its 20 saves for the hour.";
            _notices?.Report(new MemoryNotice(MemoryCodes.MemoryRateLimited, sentence));
            return new MemoryMutation(false, MemoryCodes.MemoryRateLimited, sentence, null);
        }

        if (_entries.Count(entry => string.Equals(entry.AccountId, scope.AccountId, StringComparison.Ordinal)) >= MemoryOptions.MaxEntriesPerAccount)
        {
            const string sentence = "This was not saved. This account has 1000 memory entries.";
            _notices?.Report(new MemoryNotice(MemoryCodes.MemoryCapReached, sentence));
            return new MemoryMutation(false, MemoryCodes.MemoryCapReached, sentence, null);
        }

        var decision = MemoryContentPolicy.Evaluate(draft.Topic, draft.Content, draft.Category, Settings(scope.AccountId).IncludeSensitive);
        if (decision.Verdict == MemoryPolicyVerdict.Allowed)
        {
            return null;
        }

        var code = decision.Verdict == MemoryPolicyVerdict.RefusedNeverSave ? MemoryCodes.MemoryRefusedNeverSave : MemoryCodes.MemoryRefusedSensitive;
        _notices?.Report(new MemoryNotice(code, decision.Sentence!));
        return new MemoryMutation(false, code, decision.Sentence, null);
    }

    private MemorySettings Settings(string accountId) =>
        _settings.TryGetValue(accountId, out var settings) ? settings : new MemorySettings(false, false, false);

    private static MemoryMutation PausedWrite() =>
        new(false, MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.", null);

    private static MemoryRecall PausedRecall() =>
        new(false, MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.", []);
}

/// <summary>Scope and simple-configuration search shared by the in-memory store and the brief.</summary>
public static class MemorySearch
{
    /// <summary>Returns whether the entry is visible to the scope.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>True when the entry is in scope.</returns>
    public static bool InScope(MemoryEntry entry, MemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(scope);
        if (!string.Equals(entry.AccountId, scope.AccountId, StringComparison.Ordinal))
        {
            return false;
        }

        if (scope.ProjectId is null)
        {
            return entry.ProjectId is null;
        }

        if (string.Equals(entry.ProjectId, scope.ProjectId, StringComparison.Ordinal))
        {
            return true;
        }

        return scope.UseAccountMemory && entry.ProjectId is null;
    }

    /// <summary>Returns whether every query word appears in the topic or content. An empty query matches.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="query">The query.</param>
    /// <returns>True when the entry matches.</returns>
    public static bool Matches(MemoryEntry entry, string? query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var haystack = (entry.Topic ?? string.Empty) + " " + entry.Content;
        foreach (var token in Tokens(query))
        {
            if (!haystack.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Maps an entry to the wire record. The account id is omitted.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The record.</returns>
    public static MemoryEntryRecord ToRecord(MemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new MemoryEntryRecord
        {
            Id = entry.Id,
            ProjectId = entry.ProjectId,
            Topic = entry.Topic,
            Content = entry.Content,
            Category = entry.Category,
            SourceSession = entry.SourceSession,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
        };
    }

    /// <summary>Splits a query the way the simple text-search configuration does: words, no stemming.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The words.</returns>
    public static IEnumerable<string> Tokens(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var start = -1;
        for (var i = 0; i <= query.Length; i++)
        {
            var end = i == query.Length || !char.IsLetterOrDigit(query[i]);
            if (!end && start < 0)
            {
                start = i;
            }

            if (end && start >= 0)
            {
                yield return query[start..i];
                start = -1;
            }
        }
    }
}
