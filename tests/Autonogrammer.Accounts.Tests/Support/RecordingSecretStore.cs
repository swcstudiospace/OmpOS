using System.Collections.Concurrent;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>
/// An in-memory <see cref="ISecretStore"/> that journals its operations and can be told to fail, so a test can assert that the rotated refresh token is
/// written before the new session is published.
/// </summary>
internal sealed class RecordingSecretStore(Journal? journal = null) : ISecretStore
{
    private readonly InMemorySessionSecretStore _inner = new();
    private readonly ConcurrentQueue<string> _events = new();
    private TaskCompletionSource? _getGate;
    private TaskCompletionSource? _getReached;
    private int _failRemoveCount;

    /// <summary>Gets or sets a value indicating whether <see cref="SetAsync"/> throws.</summary>
    public bool FailSet { get; set; }

    /// <summary>Gets or sets a callback run at the start of every write, before it takes effect.</summary>
    public Action? BeforeSet { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="GetAsync"/> throws.</summary>
    public bool FailGet { get; set; }

    /// <summary>Makes the next <paramref name="count"/> <see cref="RemoveAsync"/> calls throw before it works again; use <see cref="int.MaxValue"/> for a store that never removes.</summary>
    /// <param name="count">How many removals fail.</param>
    public void FailRemovals(int count) => Volatile.Write(ref _failRemoveCount, count);

    /// <summary>Gets the operations seen so far, as <c>set:name</c>, <c>get:name</c> and <c>remove:name</c>.</summary>
    public IReadOnlyList<string> Events => [.. _events];

    /// <inheritdoc />
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        Record("set:" + name);
        BeforeSet?.Invoke();
        return FailSet ? Task.FromException(new SecretStoreException("The test store refuses writes.")) : _inner.SetAsync(name, secret, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        Record("get:" + name);
        if (FailGet)
        {
            throw new SecretStoreException("The test store refuses reads.");
        }

        // The value is read before the hold, so a held read returns what was stored when it began, as a slow keyring would.
        SecretString? value = await _inner.GetAsync(name, cancellationToken).ConfigureAwait(false);
        TaskCompletionSource? gate = Volatile.Read(ref _getGate);
        if (gate is not null)
        {
            _ = _getReached?.TrySetResult();
            await gate.Task.ConfigureAwait(false);
        }

        return value;
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        Record("remove:" + name);
        if (Interlocked.Decrement(ref _failRemoveCount) >= 0)
        {
            return Task.FromException<bool>(new SecretStoreException("The test store refuses removals."));
        }

        _ = Interlocked.Exchange(ref _failRemoveCount, 0);
        return _inner.RemoveAsync(name, cancellationToken);
    }

    /// <summary>Makes every following <see cref="GetAsync"/> wait, after it has read its value, until <see cref="ReleaseGets"/>.</summary>
    /// <returns>A task that completes when a read is waiting.</returns>
    public Task HoldGets()
    {
        _getReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _getGate, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        return _getReached.Task;
    }

    /// <summary>Lets the reads held by <see cref="HoldGets"/> return.</summary>
    public void ReleaseGets() => Volatile.Read(ref _getGate)?.TrySetResult();

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) => _inner.ListNamesAsync(cancellationToken);

    /// <summary>Reads the stored session text for an assertion, without journaling it.</summary>
    /// <returns>The JSON, or null when nothing is stored.</returns>
    public async Task<string?> ReadSessionJsonAsync()
    {
        SecretString? secret = await _inner.GetAsync(AccountSessionManager.SecretName, CancellationToken.None).ConfigureAwait(false);
        return secret?.Reveal();
    }

    /// <summary>Writes a stored session as another process or an earlier run would have, without journaling it.</summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>A task that completes once it is stored.</returns>
    public Task SeedAsync(string json) => _inner.SetAsync(AccountSessionManager.SecretName, new SecretString(json), CancellationToken.None);

    private void Record(string entry)
    {
        _events.Enqueue(entry);
        journal?.Add(entry);
    }
}
