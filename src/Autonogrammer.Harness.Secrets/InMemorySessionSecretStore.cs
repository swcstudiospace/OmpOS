using System.Collections.Concurrent;
using System.Diagnostics;

namespace Autonogrammer.Harness.Secrets;

/// <summary>
/// Keeps secrets in process memory for the lifetime of one session. Nothing is written to disk, and the contents
/// vanish when the process ends or the instance is collected.
/// </summary>
/// <remarks>
/// Rendering the store, including in a debugger, shows only the number of stored secrets, never a name or a value.
/// Like <see cref="SecretString"/>, it does not guarantee that a value is absent from process memory after removal:
/// strings are immutable and the runtime may copy them.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class InMemorySessionSecretStore : ISecretStore
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly ConcurrentDictionary<string, SecretString> _secrets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        Exception? invalid = InvalidName(name);
        if (invalid is not null)
        {
            return Task.FromException(invalid);
        }

        if (secret is null)
        {
            return Task.FromException(new ArgumentNullException(nameof(secret)));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        _secrets[name] = secret;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        Exception? invalid = InvalidName(name);
        if (invalid is not null)
        {
            return Task.FromException<SecretString?>(invalid);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<SecretString?>(cancellationToken);
        }

        return Task.FromResult(_secrets.TryGetValue(name, out SecretString? secret) ? secret : null);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        Exception? invalid = InvalidName(name);
        if (invalid is not null)
        {
            return Task.FromException<bool>(invalid);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        return Task.FromResult(_secrets.TryRemove(name, out _));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<string>>(cancellationToken);
        }

        string[] names = [.. _secrets.Keys];
        Array.Sort(names, StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    /// <summary>Returns the number of stored secrets. Neither names nor values are included.</summary>
    /// <returns>A short description naming the store and its secret count.</returns>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{nameof(InMemorySessionSecretStore)} (secrets: {_secrets.Count})");

    private static Exception? InvalidName(string? name)
    {
        if (name is null)
        {
            return new ArgumentNullException(nameof(name));
        }

        return string.IsNullOrWhiteSpace(name)
            ? new ArgumentException("A secret name must not be empty or whitespace-only.", nameof(name))
            : null;
    }
}
