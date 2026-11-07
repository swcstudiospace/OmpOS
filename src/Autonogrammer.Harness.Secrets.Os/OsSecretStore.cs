using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The operating system secret store of the current platform: Secret Service through libsecret on Linux, Keychain Services on macOS and
/// DPAPI on Windows. On any other platform every operation fails as <see cref="SecretStoreFailure.Unavailable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is loaded or contacted until a method is called, so creating the store on a device that has no keyring is free and a missing
/// service is reported by the first call. A failed call throws <see cref="SecretStoreException"/> whose <see cref="SecretStoreException.Failure"/>
/// says why: <see cref="SecretStoreFailure.Unavailable"/> (no service or library), <see cref="SecretStoreFailure.Locked"/>,
/// <see cref="SecretStoreFailure.Denied"/>, <see cref="SecretStoreFailure.Corrupt"/> or <see cref="SecretStoreFailure.Other"/>. Messages and logs never
/// contain a name, a value or a native error message.
/// </para>
/// <para>
/// Every call that can block on an unlock prompt runs off the caller's thread. A prompt the person never answers keeps the call waiting until
/// the token is cancelled (Linux) or the person answers (macOS).
/// </para>
/// </remarks>
public sealed class OsSecretStore : ISecretStore
{
    private readonly ISecretStore _backend;

    /// <summary>Creates the store of the current platform.</summary>
    /// <param name="logger">Receives selection and operation messages (EventIds 3050 to 3069).</param>
    /// <param name="timeProvider">Measures operation durations for the log. The system clock when null.</param>
    public OsSecretStore(ILogger<OsSecretStore>? logger = null, TimeProvider? timeProvider = null)
        : this(Detect(), logger, timeProvider)
    {
    }

    /// <summary>Creates the store of a given platform, so a test can see which backend each platform selects.</summary>
    /// <param name="platform">The platform to select a backend for.</param>
    /// <param name="logger">Receives selection and operation messages.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    internal OsSecretStore(Platform platform, ILogger? logger, TimeProvider? timeProvider)
    {
        _backend = CreateBackend(platform, logger, timeProvider);
    }

    /// <summary>The platforms the dispatcher distinguishes.</summary>
    internal enum Platform
    {
        /// <summary>A platform without an operating system secret store.</summary>
        Unsupported,

        /// <summary>Linux: libsecret.</summary>
        Linux,

        /// <summary>macOS: Keychain Services.</summary>
        MacOs,

        /// <summary>Windows: DPAPI.</summary>
        Windows,
    }

    /// <summary>Gets the backend this store delegates to.</summary>
    internal ISecretStore Backend => _backend;

    /// <summary>Returns the platform the process runs on.</summary>
    /// <returns>The detected platform.</returns>
    internal static Platform Detect() =>
        OperatingSystem.IsLinux() ? Platform.Linux
        : OperatingSystem.IsMacOS() ? Platform.MacOs
        : OperatingSystem.IsWindows() ? Platform.Windows
        : Platform.Unsupported;

    /// <summary>Creates the backend of a platform.</summary>
    /// <param name="platform">The platform.</param>
    /// <param name="logger">Receives selection and operation messages.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    /// <returns>The platform's store.</returns>
    internal static ISecretStore CreateBackend(Platform platform, ILogger? logger, TimeProvider? timeProvider)
    {
        switch (platform)
        {
            case Platform.Linux:
                OsStoreLogMessages.StoreSelected(logger, "libsecret");
                return new LinuxLibsecretStore(logger, timeProvider);
            case Platform.MacOs:
                OsStoreLogMessages.StoreSelected(logger, "keychain");
                return new MacosKeychainStore(logger, timeProvider);
            case Platform.Windows:
                OsStoreLogMessages.StoreSelected(logger, "dpapi");
                return new WindowsDpapiStore(logger, timeProvider);
            default:
                OsStoreLogMessages.StoreUnsupported(logger);
                return new UnavailableSecretStore();
        }
    }

    /// <inheritdoc />
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken) => _backend.SetAsync(name, secret, cancellationToken);

    /// <inheritdoc />
    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken) => _backend.GetAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken) => _backend.RemoveAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) => _backend.ListNamesAsync(cancellationToken);
}
