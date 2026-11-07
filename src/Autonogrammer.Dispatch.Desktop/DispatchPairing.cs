using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Pairing state. The device's P-256 private key and its credential live only in <see cref="ISecretStore"/>; the non-secret pairing
/// record (device, account and hub keys) is a state file. Any secret-store failure refuses pairing with
/// <see cref="DispatchKeyStoreUnavailableException"/>.
/// </summary>
public sealed class DispatchPairing
{
    /// <summary>The prefix of the device key secret name. The device identifier follows.</summary>
    public const string DeviceKeyPrefix = "autonogrammer.dispatch.device-key.";

    /// <summary>The prefix of the device credential secret name. The device identifier follows.</summary>
    public const string CredentialPrefix = "autonogrammer.dispatch.device-credential.";

    private readonly ISecretStore _secrets;
    private readonly string _pairingPath;

    /// <summary>Creates the pairing state.</summary>
    /// <param name="secrets">The operating-system secret store.</param>
    /// <param name="pairingPath">The non-secret pairing record.</param>
    public DispatchPairing(ISecretStore secrets, string pairingPath)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _pairingPath = pairingPath ?? throw new ArgumentNullException(nameof(pairingPath));
    }

    /// <summary>Returns the secret name of a device's private key.</summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The name.</returns>
    public static string DeviceKeyName(string deviceId) => DeviceKeyPrefix + deviceId;

    /// <summary>Returns the secret name of a device's credential.</summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The name.</returns>
    public static string CredentialName(string deviceId) => CredentialPrefix + deviceId;

    /// <summary>
    /// Generates the device's P-256 key pair and stores the private key in the secret store. Returns the public key for the pairing request.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The public key as SubjectPublicKeyInfo DER, base64 encoded.</returns>
    /// <exception cref="DispatchKeyStoreUnavailableException">The secret store refused the private key. Pairing must not continue.</exception>
    public async Task<string> EnrollDeviceKeyAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = key.ExportPkcs8PrivateKey();
        var secret = new SecretString(Convert.ToBase64String(privateKey));
        CryptographicOperations.ZeroMemory(privateKey);
        await StoreAsync(DeviceKeyName(deviceId), secret, cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>Stores the credential the hub issued and writes the pairing record. The credential is stored first.</summary>
    /// <param name="paired">The pairing the hub confirmed.</param>
    /// <param name="credential">The device credential, a compact JWT.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when both are stored.</returns>
    public async Task CompleteAsync(DispatchPairedDevice paired, SecretString credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paired);
        ArgumentNullException.ThrowIfNull(credential);
        await StoreAsync(CredentialName(paired.DeviceId), credential, cancellationToken).ConfigureAwait(false);
        DispatchStateFile.WriteAtomic(_pairingPath, JsonSerializer.Serialize(paired, DispatchDesktopJson.Default.DispatchPairedDevice));
    }

    /// <summary>Reads the pairing record.</summary>
    /// <returns>The pairing, or null when this desktop is not paired.</returns>
    /// <exception cref="InvalidDataException">The record exists but is unreadable.</exception>
    public DispatchPairedDevice? LoadPairing()
    {
        var text = DispatchStateFile.ReadTextOrNull(_pairingPath);
        if (text is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(text, DispatchDesktopJson.Default.DispatchPairedDevice)
                ?? throw new InvalidDataException("The dispatch pairing record is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The dispatch pairing record is not valid JSON.", ex);
        }
    }

    /// <summary>Reports whether the device's private key can be read. A store failure is an error, not a false answer.</summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when a private key is stored.</returns>
    /// <exception cref="DispatchKeyStoreUnavailableException">The secret store cannot be read.</exception>
    public async Task<bool> HasDeviceKeyAsync(string deviceId, CancellationToken cancellationToken)
    {
        return await ReadAsync(DeviceKeyName(deviceId), cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>Reads the device credential.</summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The credential, or null when none is stored.</returns>
    /// <exception cref="DispatchKeyStoreUnavailableException">The secret store cannot be read.</exception>
    public Task<SecretString?> ReadCredentialAsync(string deviceId, CancellationToken cancellationToken) =>
        ReadAsync(CredentialName(deviceId), cancellationToken);

    /// <summary>Reads the expiry of a compact JWT credential from its <c>exp</c> claim. The signature is not checked here; the hub checks it.</summary>
    /// <param name="jwt">The credential.</param>
    /// <returns>The expiry, or null when the claim cannot be read.</returns>
    public static DateTimeOffset? ReadCredentialExpiry(string jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        var parts = jwt.Split('.');
        if (parts.Length != 3 || !DispatchBase64Url.TryDecode(parts[1], out var payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private async Task StoreAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        try
        {
            await _secrets.SetAsync(name, secret, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DispatchKeyStoreUnavailableException(ex);
        }
    }

    private async Task<SecretString?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await _secrets.GetAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DispatchKeyStoreUnavailableException(ex);
        }
    }
}
