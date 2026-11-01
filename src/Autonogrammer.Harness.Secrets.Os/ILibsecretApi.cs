namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The four libsecret operations in managed types. Calls block. A failure reported by libsecret is a <see cref="SecretStoreException"/> built by
/// <see cref="LinuxLibsecretStore.Failed"/>; a cancelled call is an <see cref="OperationCanceledException"/>; a missing library is a
/// <see cref="DllNotFoundException"/>. The native implementation is <see cref="LibsecretNativeApi"/>; tests substitute an in-memory fake.
/// </summary>
internal interface ILibsecretApi
{
    /// <summary>Stores a secret under the attributes <c>{app: autonogrammer, name}</c>, replacing an existing one.</summary>
    /// <param name="name">The name.</param>
    /// <param name="secret">The secret.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    void Store(string name, SecretString secret, CancellationToken cancellationToken);

    /// <summary>Looks a secret up.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The secret, or null when none is stored.</returns>
    SecretString? Lookup(string name, CancellationToken cancellationToken);

    /// <summary>Removes a secret.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a secret was removed.</returns>
    bool Clear(string name, CancellationToken cancellationToken);

    /// <summary>Lists the names of every item with the attribute <c>app: autonogrammer</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The names, in any order.</returns>
    IReadOnlyList<string> Search(CancellationToken cancellationToken);
}
