namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The store of a platform that has no operating system secret store: every operation checks its arguments, then fails as
/// <see cref="SecretStoreFailure.Unavailable"/> through the returned task.
/// </summary>
internal sealed class UnavailableSecretStore : ISecretStore
{
    /// <inheritdoc />
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken) =>
        FailAsync<bool>(() =>
        {
            StoreGuards.ValidateName(name);
            ArgumentNullException.ThrowIfNull(secret);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken) =>
        FailAsync<SecretString?>(() => StoreGuards.ValidateName(name), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken) =>
        FailAsync<bool>(() => StoreGuards.ValidateName(name), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) =>
        FailAsync<IReadOnlyList<string>>(static () => { }, cancellationToken);

    private static Task<T> FailAsync<T>(Action checkArguments, CancellationToken cancellationToken)
    {
        try
        {
            checkArguments();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Task.FromException<T>(exception);
        }

        return Task.FromException<T>(new SecretStoreException(
            StoreGuards.Describe("operating system secret store", SecretStoreFailure.Unavailable),
            SecretStoreFailure.Unavailable));
    }
}
