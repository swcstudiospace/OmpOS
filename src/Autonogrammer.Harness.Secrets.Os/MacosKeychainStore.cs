using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="ISecretStore"/> over macOS Keychain Services: generic password items with service <c>Autonogrammer</c> and the secret's name as the
/// account. The store never starts <c>/usr/bin/security</c>, whose <c>add-generic-password</c> puts the secret on its command line. An unsigned
/// build may be asked for permission on every launch; a refused prompt is <see cref="SecretStoreFailure.Denied"/>.
/// </summary>
internal sealed class MacosKeychainStore : BlockingSecretStore
{
    // OSStatus values, from Security/SecBase.h.
    internal const int Success = 0;
    internal const int UserCanceled = -128;
    internal const int NotAvailable = -25291;
    internal const int ReadOnly = -25292;
    internal const int AuthFailed = -25293;
    internal const int NoSuchKeychain = -25294;
    internal const int InvalidKeychain = -25295;
    internal const int DuplicateItem = -25299;
    internal const int ItemNotFound = -25300;
    internal const int NoDefaultKeychain = -25307;
    internal const int InteractionNotAllowed = -25308;
    internal const int Decode = -26275;
    internal const int MissingEntitlement = -34018;

    private const string Description = "macOS Keychain";

    private readonly IKeychainApi _api;

    /// <summary>Creates a store over the native Keychain binding.</summary>
    /// <param name="logger">Receives operation messages. They carry no secret name or value.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    public MacosKeychainStore(ILogger? logger = null, TimeProvider? timeProvider = null)
        : this(new KeychainNativeApi(), logger, timeProvider)
    {
    }

    /// <summary>Creates a store over a given Keychain binding, so a test can substitute a fake.</summary>
    /// <param name="api">The Keychain operations.</param>
    /// <param name="logger">Receives operation messages.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    internal MacosKeychainStore(IKeychainApi api, ILogger? logger, TimeProvider? timeProvider)
        : base(logger, timeProvider)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    protected override string StoreName => "keychain";

    /// <inheritdoc />
    protected override string StoreDescription => Description;

    /// <summary>Classifies an <c>OSStatus</c>.</summary>
    /// <param name="status">A non-success status.</param>
    /// <returns>The failure kind.</returns>
    internal static SecretStoreFailure ClassifyStatus(int status) => status switch
    {
        InteractionNotAllowed => SecretStoreFailure.Locked,
        UserCanceled or AuthFailed or ReadOnly or MissingEntitlement => SecretStoreFailure.Denied,
        NotAvailable or NoSuchKeychain or NoDefaultKeychain => SecretStoreFailure.Unavailable,
        InvalidKeychain or Decode => SecretStoreFailure.Corrupt,
        _ => SecretStoreFailure.Other,
    };

    /// <inheritdoc />
    protected override void Put(string name, SecretString secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int status = _api.Add(name, secret);
        if (status == DuplicateItem)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = _api.Update(name, secret);
        }

        ThrowIfFailed(status);
    }

    /// <inheritdoc />
    protected override SecretString? Fetch(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int status = _api.Copy(name, out SecretString? secret);
        if (status == ItemNotFound)
        {
            return null;
        }

        ThrowIfFailed(status);
        return secret;
    }

    /// <inheritdoc />
    protected override bool Delete(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int status = _api.Delete(name);
        if (status == ItemNotFound)
        {
            return false;
        }

        ThrowIfFailed(status);
        return true;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> Enumerate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int status = _api.List(out IReadOnlyList<string> accounts);
        if (status == ItemNotFound)
        {
            return [];
        }

        ThrowIfFailed(status);
        return accounts;
    }

    /// <inheritdoc />
    protected override SecretStoreFailure? Classify(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException ? SecretStoreFailure.Unavailable : null;

    private static void ThrowIfFailed(int status)
    {
        if (status != Success)
        {
            SecretStoreFailure failure = ClassifyStatus(status);
            throw new SecretStoreException(StoreGuards.Describe(Description, failure), failure);
        }
    }
}
