using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="ISecretStore"/> over the Linux Secret Service through libsecret. Items carry the attributes <c>app = autonogrammer</c> and
/// <c>name = &lt;name&gt;</c>. The Secret Service has no per-application isolation: any process of the same unlocked session can read an item.
/// </summary>
internal sealed class LinuxLibsecretStore : BlockingSecretStore
{
    // GIO and D-Bus codes, from gio/gioenums.h and gio/gdbusenums.h.
    private const int GioPermissionDenied = 14;
    private const int DBusIoError = 5;
    private const int DBusServiceUnknown = 2;
    private const int DBusNameHasNoOwner = 3;
    private const int DBusNoReply = 4;
    private const int DBusBadAddress = 6;
    private const int DBusAccessDenied = 9;
    private const int DBusAuthFailed = 10;
    private const int DBusNoServer = 11;
    private const int DBusTimeout = 12;
    private const int DBusNoNetwork = 13;
    private const int DBusDisconnected = 15;

    // libsecret codes, from libsecret/secret-error.h.
    private const int SecretIsLocked = 2;
    private const int SecretNoSuchObject = 3;

    private const string Description = "Linux Secret Service";

    private readonly ILibsecretApi _api;

    /// <summary>Creates a store over libsecret's native binding.</summary>
    /// <param name="logger">Receives operation messages. They carry no secret name, value or native message.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    public LinuxLibsecretStore(ILogger? logger = null, TimeProvider? timeProvider = null)
        : this(new LibsecretNativeApi(), logger, timeProvider)
    {
    }

    /// <summary>Creates a store over a given libsecret binding, so a test can substitute a fake.</summary>
    /// <param name="api">The libsecret operations.</param>
    /// <param name="logger">Receives operation messages.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    internal LinuxLibsecretStore(ILibsecretApi api, ILogger? logger, TimeProvider? timeProvider)
        : base(logger, timeProvider)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    protected override string StoreName => "libsecret";

    /// <inheritdoc />
    protected override string StoreDescription => Description;

    /// <summary>Classifies a libsecret <c>GError</c> by its GLib domain and code.</summary>
    /// <param name="domain">The error domain.</param>
    /// <param name="code">The code within the domain.</param>
    /// <returns>The failure kind.</returns>
    internal static SecretStoreFailure ClassifyFailure(LibsecretErrorDomain domain, int code) => domain switch
    {
        LibsecretErrorDomain.Secret => code switch
        {
            SecretIsLocked => SecretStoreFailure.Locked,
            SecretNoSuchObject => SecretStoreFailure.Unavailable,
            _ => SecretStoreFailure.Other,
        },
        LibsecretErrorDomain.DBus => code switch
        {
            DBusServiceUnknown or DBusNameHasNoOwner or DBusNoReply or DBusIoError or DBusBadAddress or DBusNoServer or DBusTimeout or DBusNoNetwork or DBusDisconnected => SecretStoreFailure.Unavailable,
            DBusAccessDenied or DBusAuthFailed => SecretStoreFailure.Denied,
            _ => SecretStoreFailure.Other,
        },
        // Every other GIO failure, including an unregistered remote D-Bus error (G_IO_ERROR_DBUS_ERROR, for example a service whose activation fails),
        // means the service could not be reached.
        LibsecretErrorDomain.Gio => code switch
        {
            GioPermissionDenied => SecretStoreFailure.Denied,
            _ => SecretStoreFailure.Unavailable,
        },
        _ => SecretStoreFailure.Other,
    };

    /// <summary>Builds the exception for a libsecret <c>GError</c>. Only the classification is kept: the domain, the code and libsecret's free-text message are dropped.</summary>
    /// <param name="domain">The error domain.</param>
    /// <param name="code">The code within the domain.</param>
    /// <returns>A <see cref="SecretStoreException"/> carrying the classified failure.</returns>
    internal static SecretStoreException Failed(LibsecretErrorDomain domain, int code)
    {
        SecretStoreFailure failure = ClassifyFailure(domain, code);
        return new SecretStoreException(StoreGuards.Describe(Description, failure), failure);
    }

    /// <inheritdoc />
    protected override void Put(string name, SecretString secret, CancellationToken cancellationToken) => _api.Store(name, secret, cancellationToken);

    /// <inheritdoc />
    protected override SecretString? Fetch(string name, CancellationToken cancellationToken) => _api.Lookup(name, cancellationToken);

    /// <inheritdoc />
    protected override bool Delete(string name, CancellationToken cancellationToken) => _api.Clear(name, cancellationToken);

    /// <inheritdoc />
    protected override IReadOnlyList<string> Enumerate(CancellationToken cancellationToken) => _api.Search(cancellationToken);

    /// <inheritdoc />
    protected override SecretStoreFailure? Classify(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException ? SecretStoreFailure.Unavailable : null;
}
