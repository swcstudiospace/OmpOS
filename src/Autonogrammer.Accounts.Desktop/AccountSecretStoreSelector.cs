using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>
/// The <see cref="ISecretStore"/> the desktop head gives <see cref="AccountSessionManager"/>. It asks the system secret store only when
/// <see cref="HasStoredSession"/> is true or a sign-in just wrote the session, so the first launch of a person who never signs in makes no keyring call.
/// When the system store fails with <see cref="SecretStoreFailure.Unavailable"/>, <see cref="SecretStoreFailure.Denied"/> or
/// <see cref="SecretStoreFailure.Locked"/> (a dismissed unlock prompt), it keeps the session in memory and reports every write as that failure, which the
/// manager shows as the notice <c>SessionNotRemembered</c>. <see cref="RememberWithoutSystemProtectionAsync"/> is the person's consent to an owner-only
/// file instead (D-26).
/// </summary>
/// <remarks>
/// A device private key (name starting <see cref="DeviceKeyPrefix"/>) is never kept in memory or in the file fallback: it goes to the system store or
/// fails with that store's own failure, which Dispatch reports as <c>KeyStoreUnavailable</c>. It does not count as a stored session and the selector does
/// not degrade because of it.
/// </remarks>
public sealed class AccountSecretStoreSelector : ISecretStore, IDisposable
{
    /// <summary>The prefix of a Dispatch device key name, <c>autonogrammer.dispatch.device-key</c> (the device id follows after a dot).</summary>
    public const string DeviceKeyPrefix = "autonogrammer.dispatch.device-key";

    private const string NotRememberedMessage = "The session is kept in memory only because the system secret store is not available.";

    private readonly ISecretStore _os;
    private readonly ISecretStore _file;
    private readonly InMemorySessionSecretStore _memory = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<AccountSecretStoreSelector>? _logger;
    private State _state;
    private SecretStoreFailure _degradedFailure = SecretStoreFailure.Unavailable;

    /// <summary>Initializes a new instance of the <see cref="AccountSecretStoreSelector"/> class.</summary>
    /// <param name="osStore">The system secret store, for example <c>OsSecretStore</c>. It is not touched until a session is stored or a sign-in writes one.</param>
    /// <param name="fileStore">The owner-only file store used after consent, for example <c>OwnerOnlyFileSecretStore</c>.</param>
    /// <param name="hasStoredSession">The value of <see cref="AccountSettingsKeys.HasStoredSession"/>.</param>
    /// <param name="allowFileSecretStore">The value of <see cref="AccountSettingsKeys.AllowFileSecretStore"/>.</param>
    /// <param name="logger">An optional logger. It never receives a secret name, value or native error text.</param>
    public AccountSecretStoreSelector(ISecretStore osStore, ISecretStore fileStore, bool hasStoredSession, bool allowFileSecretStore, ILogger<AccountSecretStoreSelector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(osStore);
        ArgumentNullException.ThrowIfNull(fileStore);
        _os = osStore;
        _file = fileStore;
        _logger = logger;
        _state = new State(
            allowFileSecretStore ? AccountSecretStoreMode.OwnerOnlyFile : hasStoredSession ? AccountSecretStoreMode.OperatingSystem : AccountSecretStoreMode.NotAsked,
            hasStoredSession,
            allowFileSecretStore);
    }

    /// <summary>Occurs after <see cref="Mode"/>, <see cref="HasStoredSession"/> or <see cref="AllowFileSecretStore"/> changed. The head stores the two settings then.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets where the session is kept now.</summary>
    public AccountSecretStoreMode Mode => Volatile.Read(ref _state).Mode;

    /// <summary>Gets a value indicating whether a session is durably stored. It is the value of <see cref="AccountSettingsKeys.HasStoredSession"/> to keep.</summary>
    public bool HasStoredSession => Volatile.Read(ref _state).HasStoredSession;

    /// <summary>Gets a value indicating whether the person consented to the file fallback. It is the value of <see cref="AccountSettingsKeys.AllowFileSecretStore"/> to keep.</summary>
    public bool AllowFileSecretStore => Volatile.Read(ref _state).AllowFileSecretStore;

    /// <summary>
    /// The consent action "Remember sign-in on this device without system protection": switches to the owner-only file store, moves the secrets held in memory
    /// into it and sets <see cref="AllowFileSecretStore"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the move.</param>
    /// <returns>A task that completes once the file holds the secrets. It faults with the file store's exception, leaving the memory-only state, when the file cannot be written.</returns>
    public async Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        State before = Volatile.Read(ref _state);
        try
        {
            await RememberCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyIfChanged(before);
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(secret);
        if (IsDeviceKey(name))
        {
            await _os.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
            return;
        }

        State before = Volatile.Read(ref _state);
        try
        {
            await SetCoreAsync(name, secret, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyIfChanged(before);
        }
    }

    /// <inheritdoc />
    public async Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (IsDeviceKey(name))
        {
            return await _os.GetAsync(name, cancellationToken).ConfigureAwait(false);
        }

        State before = Volatile.Read(ref _state);
        try
        {
            return await GetCoreAsync(name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyIfChanged(before);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (IsDeviceKey(name))
        {
            return await _os.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
        }

        State before = Volatile.Read(ref _state);
        try
        {
            return await RemoveCoreAsync(name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyIfChanged(before);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
    {
        State before = Volatile.Read(ref _state);
        try
        {
            return await ListCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyIfChanged(before);
        }
    }

    /// <summary>Releases the gate.</summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>Names the type. No path, name or value is included.</summary>
    /// <returns>The type name.</returns>
    public override string ToString() => nameof(AccountSecretStoreSelector);

    private static bool IsDeviceKey(string name) => name.StartsWith(DeviceKeyPrefix, StringComparison.Ordinal);

    private static bool IsDegradable(SecretStoreFailure failure) =>
        failure is SecretStoreFailure.Unavailable or SecretStoreFailure.Denied or SecretStoreFailure.Locked;

    private async Task RememberCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State before = _state;
            if (before.AllowFileSecretStore)
            {
                return;
            }

            IReadOnlyList<string> names = await _memory.ListNamesAsync(cancellationToken).ConfigureAwait(false);
            int moved = 0;
            foreach (string name in names)
            {
                SecretString? secret = await _memory.GetAsync(name, cancellationToken).ConfigureAwait(false);
                if (secret is null)
                {
                    continue;
                }

                await _file.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
                _ = await _memory.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
                moved++;
            }

            Publish(new State(AccountSecretStoreMode.OwnerOnlyFile, before.HasStoredSession || moved > 0, AllowFileSecretStore: true));
            DesktopLogMessages.SecretStoreConsentGranted(_logger);
            DesktopLogMessages.SecretStoreMigrated(_logger, moved);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task SetCoreAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State before = _state;
            switch (before.Mode)
            {
                case AccountSecretStoreMode.OwnerOnlyFile:
                    await _file.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
                    Publish(before with { HasStoredSession = true });
                    return;
                case AccountSecretStoreMode.MemoryOnly:
                    await _memory.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
                    throw new SecretStoreException(NotRememberedMessage, _degradedFailure);
                default:
                    await SetInSystemStoreAsync(before, name, secret, cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task SetInSystemStoreAsync(State before, string name, SecretString secret, CancellationToken cancellationToken)
    {
        try
        {
            await _os.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
            Publish(before with { Mode = AccountSecretStoreMode.OperatingSystem, HasStoredSession = true });
        }
        catch (SecretStoreException exception) when (IsDegradable(exception.Failure))
        {
            Degrade(before, exception.Failure);
            await _memory.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
            throw new SecretStoreException(NotRememberedMessage, exception.Failure, exception);
        }
    }

    private async Task<SecretString?> GetCoreAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State before = _state;
            switch (before.Mode)
            {
                case AccountSecretStoreMode.OwnerOnlyFile:
                    return await _file.GetAsync(name, cancellationToken).ConfigureAwait(false);
                case AccountSecretStoreMode.MemoryOnly:
                    return await _memory.GetAsync(name, cancellationToken).ConfigureAwait(false);
                case AccountSecretStoreMode.NotAsked:
                    DesktopLogMessages.FirstLaunchSkipped(_logger);
                    return null;
                default:
                    try
                    {
                        return await _os.GetAsync(name, cancellationToken).ConfigureAwait(false);
                    }
                    catch (SecretStoreException exception) when (IsDegradable(exception.Failure))
                    {
                        Degrade(before, exception.Failure);
                        return null;
                    }
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task<bool> RemoveCoreAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State before = _state;
            switch (before.Mode)
            {
                case AccountSecretStoreMode.OwnerOnlyFile:
                    bool removedFromFile = await _file.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
                    Publish(before with { HasStoredSession = false });
                    return removedFromFile;
                case AccountSecretStoreMode.MemoryOnly:
                    return await _memory.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
                case AccountSecretStoreMode.NotAsked:
                    return false;
                default:
                    try
                    {
                        bool removed = await _os.RemoveAsync(name, cancellationToken).ConfigureAwait(false);
                        Publish(before with { HasStoredSession = false });
                        return removed;
                    }
                    catch (SecretStoreException exception) when (IsDegradable(exception.Failure))
                    {
                        // The entry could not be reached to remove it; nothing is read from the system store again until a sign-in writes it.
                        Degrade(before, exception.Failure);
                        throw;
                    }
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task<IReadOnlyList<string>> ListCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State before = _state;
            switch (before.Mode)
            {
                case AccountSecretStoreMode.OwnerOnlyFile:
                    return await _file.ListNamesAsync(cancellationToken).ConfigureAwait(false);
                case AccountSecretStoreMode.MemoryOnly:
                    return await _memory.ListNamesAsync(cancellationToken).ConfigureAwait(false);
                case AccountSecretStoreMode.NotAsked:
                    return [];
                default:
                    try
                    {
                        return await _os.ListNamesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (SecretStoreException exception) when (IsDegradable(exception.Failure))
                    {
                        Degrade(before, exception.Failure);
                        return [];
                    }
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private void NotifyIfChanged(State before)
    {
        if (before != Volatile.Read(ref _state))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Publish(State next) => Volatile.Write(ref _state, next);

    private void Degrade(State before, SecretStoreFailure failure)
    {
        _degradedFailure = failure;
        Publish(before with { Mode = AccountSecretStoreMode.MemoryOnly, HasStoredSession = false });
        DesktopLogMessages.SecretStoreDegraded(_logger, failure.ToString());
    }

    private sealed record State(AccountSecretStoreMode Mode, bool HasStoredSession, bool AllowFileSecretStore);
}
