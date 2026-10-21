using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OmpOS.Sandbox.Abstractions;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Test-only <see cref="ISandboxProvider"/> decorator counting provisions and teardowns per session.
/// Delegates every call to the inner provider; the sandbox id to session id mapping is captured at
/// provision time so teardown calls (which carry only the sandbox id) can be attributed.
/// </summary>
internal sealed class CountingSandboxProvider : ISandboxProvider
{
    private readonly ISandboxProvider _inner;
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _provisions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _teardowns = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="CountingSandboxProvider"/> class.</summary>
    /// <param name="inner">The provider to delegate to.</param>
    public CountingSandboxProvider(ISandboxProvider inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _inner.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
        _sessions[result.Descriptor.Id] = result.Descriptor.SessionId;
        _provisions.AddOrUpdate(result.Descriptor.SessionId, 1, static (_, count) => count + 1);
        return result;
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        return _inner.AttachAsync(sandboxId, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        return _inner.ExecAsync(sandboxId, request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        return StreamLogsIteratorAsync(sandboxId, cursor, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        return _inner.SnapshotAsync(sandboxId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        await _inner.TeardownAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        if (_sessions.TryGetValue(sandboxId, out var sessionId))
        {
            _teardowns.AddOrUpdate(sessionId, 1, static (_, count) => count + 1);
        }
    }

    /// <summary>Gets the provision count for a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The number of provision calls attributed to the session.</returns>
    public int ProvisionsFor(string sessionId)
    {
        return _provisions.TryGetValue(sessionId, out var count) ? count : 0;
    }

    /// <summary>Gets the teardown count for a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The number of teardown calls attributed to the session.</returns>
    public int TeardownsFor(string sessionId)
    {
        return _teardowns.TryGetValue(sessionId, out var count) ? count : 0;
    }

    private async IAsyncEnumerable<string> StreamLogsIteratorAsync(
        string sandboxId,
        LogCursor? cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in _inner.StreamLogsAsync(sandboxId, cursor, cancellationToken).ConfigureAwait(false))
        {
            yield return line;
        }
    }
}
