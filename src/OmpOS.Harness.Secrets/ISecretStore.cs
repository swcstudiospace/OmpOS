namespace OmpOS.Harness.Secrets;

/// <summary>
/// Stores named secrets, such as provider keys, so that callers hold a name rather than the value. Values cross the
/// interface only as <see cref="SecretString"/>, so neither a stored value nor a value in transit can be rendered by
/// accident.
/// </summary>
/// <remarks>
/// <para>
/// Names are not secret: they are compared ordinally, are case-sensitive, and may appear in logs. A name must be neither
/// null, empty nor whitespace-only. No member includes a secret value in an exception message.
/// </para>
/// <para>
/// Every refusal, including an invalid argument or a cancelled token, is reported through the returned task;
/// implementations must not throw synchronously. Implementations must be safe for concurrent use.
/// </para>
/// <para>
/// Only the in-memory session implementation exists. It is not durable: its contents vanish with the process. A store
/// backed by the operating system (Windows credential store, macOS Keychain, Linux Secret Service) implements this
/// interface once it can be verified on a runner for that platform.
/// </para>
/// </remarks>
public interface ISecretStore
{
    /// <summary>Stores a secret under a name, replacing any secret already stored under that name.</summary>
    /// <param name="name">The name to store the secret under.</param>
    /// <param name="secret">The secret to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// A task that completes once the secret is stored. It faults with <see cref="ArgumentNullException"/> when
    /// <paramref name="name"/> or <paramref name="secret"/> is null, with <see cref="ArgumentException"/> when
    /// <paramref name="name"/> is empty or whitespace-only, and is cancelled when the token is cancelled.
    /// </returns>
    Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken);

    /// <summary>Reads the secret stored under a name.</summary>
    /// <param name="name">The name the secret was stored under.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// A task whose result is the stored secret, or null when nothing is stored under <paramref name="name"/>. It faults
    /// for an invalid name as <see cref="SetAsync"/> does and is cancelled when the token is cancelled.
    /// </returns>
    Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken);

    /// <summary>Removes the secret stored under a name.</summary>
    /// <param name="name">The name the secret was stored under.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>
    /// A task whose result is true when a secret was removed and false when nothing was stored under
    /// <paramref name="name"/>. It faults for an invalid name as <see cref="SetAsync"/> does and is cancelled when the
    /// token is cancelled.
    /// </returns>
    Task<bool> RemoveAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists the names of every stored secret. Values are never listed.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>
    /// A task whose result is a snapshot of the stored names in ordinal order, empty when the store is empty. It is
    /// cancelled when the token is cancelled.
    /// </returns>
    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken);
}
