using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.Dispatch.Devices;

/// <summary>
/// Signs the device credential with <c>Dispatch:Credentials:SigningKeyPem</c>, never the command-signing key or the worker key.
/// Audience is <c>autonogrammer-dispatch-device</c>. The PEM never appears in <see cref="ToString"/> or logs.
/// </summary>
internal sealed class DeviceCredentialIssuer : IDisposable
{
    /// <summary>The audience claim every device credential carries.</summary>
    public const string Audience = DispatchDeviceScheme.Audience;

    /// <summary>The confirmation claim that carries the device-key thumbprint.</summary>
    public const string ConfirmationClaim = "cnf";

    /// <summary>The account claim.</summary>
    public const string OwnerClaim = "own";

    private readonly ECDsa _credentialKey;
    private readonly ECDsa _commandKey;
    private readonly SigningCredentials _credentials;
    private readonly DispatchOptions _options;
    private readonly TimeProvider _time;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Initializes a new instance of the <see cref="DeviceCredentialIssuer"/> class.</summary>
    /// <param name="options">The dispatch options.</param>
    /// <param name="time">The clock that stamps <c>iat</c> and <c>exp</c>.</param>
    public DeviceCredentialIssuer(IOptions<DispatchOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _options = options.Value;
        _time = time;
        _credentialKey = Load(_options.Credentials.SigningKeyPem, "Dispatch:Credentials:SigningKeyPem");
        _commandKey = Load(_options.CommandSigning.KeyPem, "Dispatch:CommandSigning:KeyPem");
        _credentials = new SigningCredentials(new ECDsaSecurityKey(_credentialKey) { KeyId = "dispatch-credential" }, SecurityAlgorithms.EcdsaSha256);
    }

    /// <summary>Signs a credential and returns its identifier and expiry.</summary>
    /// <param name="deviceId">The device identifier. Becomes <c>sub</c>.</param>
    /// <param name="accountId">The owning account. Becomes <c>own</c>.</param>
    /// <param name="thumbprint">The SHA-256 JWK thumbprint of the device key.</param>
    /// <returns>The compact JWT, its <c>jti</c> and its expiry.</returns>
    public IssuedCredential Issue(string deviceId, string accountId, string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        var now = _time.GetUtcNow();
        var expires = now + _options.Credentials.Lifetime;
        var jti = Guid.NewGuid().ToString("N");
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Iss] = _options.Credentials.Issuer,
                [JwtRegisteredClaimNames.Aud] = Audience,
                [JwtRegisteredClaimNames.Sub] = deviceId,
                [OwnerClaim] = accountId,
                [ConfirmationClaim] = new Dictionary<string, string>(StringComparer.Ordinal) { ["jkt"] = thumbprint },
                [JwtRegisteredClaimNames.Jti] = jti,
                [JwtRegisteredClaimNames.Iat] = now.ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Exp] = expires.ToUnixTimeSeconds(),
            },
            NotBefore = now.UtcDateTime,
            SigningCredentials = _credentials,
        };
        return new IssuedCredential(_handler.CreateToken(descriptor), jti, expires);
    }

    /// <summary>Issues a replacement credential and stores its <c>jti</c>, which invalidates older copies.</summary>
    /// <param name="store">The device store.</param>
    /// <param name="deviceId">The device.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="thumbprint">The device-key thumbprint.</param>
    /// <param name="cancellationToken">Cancels the store update.</param>
    /// <returns>The new credential, or null when the account does not own the device.</returns>
    public async ValueTask<IssuedCredential?> RotateAsync(IDispatchStore store, string deviceId, string accountId, string thumbprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var issued = Issue(deviceId, accountId, thumbprint);
        var updated = await store.UpdateCredentialJtiAsync(accountId, deviceId, issued.Jti, issued.ExpiresAt, cancellationToken).ConfigureAwait(false);
        return updated ? issued : null;
    }

    /// <summary>Gets the hub verification key published at pairing.</summary>
    /// <returns>The key id, the ES256 public JWK and the validity window.</returns>
    public HubVerificationKey HubKey()
    {
        var now = _time.GetUtcNow();
        var parameters = _commandKey.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X!),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y!),
            ["alg"] = "ES256",
            ["use"] = "sig",
        };
        return new HubVerificationKey("dispatch-command", jwk, now, now + _options.Credentials.Lifetime);
    }

    /// <summary>Computes the RFC 7638 thumbprint of an EC P-256 public JWK.</summary>
    /// <param name="x">The base64url X coordinate.</param>
    /// <param name="y">The base64url Y coordinate.</param>
    /// <returns>The base64url SHA-256 thumbprint.</returns>
    public static string Thumbprint(string x, string y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(x);
        ArgumentException.ThrowIfNullOrWhiteSpace(y);
        var canonical = "{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"" + x + "\",\"y\":\"" + y + "\"}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(canonical)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _credentialKey.Dispose();
        _commandKey.Dispose();
    }

    /// <inheritdoc />
    public override string ToString() => "DeviceCredentialIssuer (keys not shown)";

    private static ECDsa Load(string pem, string keyName)
    {
        var key = ECDsa.Create();
        bool valid;
        try
        {
            key.ImportFromPem(CloudOptionsValidator.NormalizePem(pem));
            valid = key.KeySize == 256 && key.ExportParameters(includePrivateParameters: true).D is { Length: > 0 };
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            valid = false;
        }

        if (!valid)
        {
            key.Dispose();
            throw new InvalidOperationException(keyName + " must be a PKCS#8 PEM private key on the P-256 curve.");
        }

        return key;
    }
    /// <summary>A signed device credential.</summary>
    /// <param name="Token">The compact JWT. Never log it.</param>
    /// <param name="Jti">The credential identifier stored on the device row.</param>
    /// <param name="ExpiresAt">The expiry.</param>
    internal sealed record IssuedCredential(string Token, string Jti, DateTimeOffset ExpiresAt);

    /// <summary>One hub verification key.</summary>
    /// <param name="Kid">The key identifier.</param>
    /// <param name="Jwk">The public JWK members.</param>
    /// <param name="NotBefore">When the key becomes valid.</param>
    /// <param name="Expires">When the key stops being valid.</param>
    internal sealed record HubVerificationKey(string Kid, IReadOnlyDictionary<string, string> Jwk, DateTimeOffset NotBefore, DateTimeOffset Expires);
}
