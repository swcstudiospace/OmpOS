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

    /// <summary>Gets or sets a value indicating whether <see cref="SetAsync"/> throws.</summary>
    public bool FailSet { get; set; }

    /// <summary>Gets or sets a callback run at the start of every write, before it takes effect.</summary>
    public Action? BeforeSet { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="GetAsync"/> throws.</summary>
    public bool FailGet { get; set; }

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
    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        Record("get:" + name);
        return FailGet ? Task.FromException<SecretString?>(new SecretStoreException("The test store refuses reads.")) : _inner.GetAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        Record("remove:" + name);
        return _inner.RemoveAsync(name, cancellationToken);
    }

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
