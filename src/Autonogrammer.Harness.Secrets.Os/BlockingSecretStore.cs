using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// Base of the stores whose native calls block: every call runs on its own thread, off the caller's thread, because an unlock prompt can
/// keep a call waiting for as long as a person takes to answer. Cancellation is passed to the native call by the concrete store.
/// </summary>
internal abstract class BlockingSecretStore(ILogger? logger, TimeProvider? timeProvider) : ISecretStore
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Gets the short store name used in log messages, for example <c>libsecret</c>.</summary>
    protected abstract string StoreName { get; }

    /// <summary>Gets the human name used in exception messages, for example <c>Linux Secret Service</c>.</summary>
    protected abstract string StoreDescription { get; }

    /// <inheritdoc />
    public async Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        ArgumentNullException.ThrowIfNull(secret);
        _ = await RunAsync(
            "Set",
            () =>
            {
                Put(name, secret, cancellationToken);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        return await RunAsync("Get", () => Fetch(name, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        return await RunAsync("Remove", () => Delete(name, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = await RunAsync("List", () => Enumerate(cancellationToken), cancellationToken).ConfigureAwait(false);
        string[] sorted = [.. names.Distinct(StringComparer.Ordinal)];
        Array.Sort(sorted, StringComparer.Ordinal);
        return sorted;
    }

    /// <summary>Stores a secret. Runs off the caller's thread; may block on a prompt.</summary>
    /// <param name="name">The validated name.</param>
    /// <param name="secret">The secret.</param>
    /// <param name="cancellationToken">Cancels the native call.</param>
    protected abstract void Put(string name, SecretString secret, CancellationToken cancellationToken);

    /// <summary>Reads a secret. Runs off the caller's thread; may block on a prompt.</summary>
    /// <param name="name">The validated name.</param>
    /// <param name="cancellationToken">Cancels the native call.</param>
    /// <returns>The secret, or null when nothing is stored under the name.</returns>
    protected abstract SecretString? Fetch(string name, CancellationToken cancellationToken);

    /// <summary>Removes a secret. Runs off the caller's thread; may block on a prompt.</summary>
    /// <param name="name">The validated name.</param>
    /// <param name="cancellationToken">Cancels the native call.</param>
    /// <returns>True when a secret was removed.</returns>
    protected abstract bool Delete(string name, CancellationToken cancellationToken);

    /// <summary>Lists the stored names. Runs off the caller's thread; may block on a prompt.</summary>
    /// <param name="cancellationToken">Cancels the native call.</param>
    /// <returns>The stored names in any order.</returns>
    protected abstract IReadOnlyList<string> Enumerate(CancellationToken cancellationToken);

    /// <summary>Classifies a failure of a native call.</summary>
    /// <param name="exception">The failure thrown by the concrete store's native layer.</param>
    /// <returns>The failure kind, or null when the failure is not one the store recognises.</returns>
    protected abstract SecretStoreFailure? Classify(Exception exception);

    private async Task<T> RunAsync<T>(string operation, Func<T> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = _time.GetTimestamp();
        T result;
        try
        {
            result = await Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);
        }
        catch (SecretStoreException exception)
        {
            OsStoreLogMessages.OperationFailed(logger, StoreName, operation, exception.Failure);
            throw;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ArgumentException))
        {
            SecretStoreFailure failure = Classify(exception) ?? SecretStoreFailure.Other;
            OsStoreLogMessages.OperationFailed(logger, StoreName, operation, failure);
            throw new SecretStoreException(StoreGuards.Describe(StoreDescription, failure), failure, exception);
        }

        OsStoreLogMessages.OperationCompleted(logger, StoreName, operation, (long)_time.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }
}
