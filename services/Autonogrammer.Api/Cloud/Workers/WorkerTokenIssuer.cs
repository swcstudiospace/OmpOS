using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Autonogrammer.Api.Cloud.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Signs the bearer token a worker requires on its WebSocket upgrade (ADR-0020 D4): ES256, <c>aud</c> <c>autonogrammer-worker</c>, <c>iss</c> from
/// <c>Cloud:Worker:Issuer</c>, <c>sub</c> the session identifier, a unique <c>jti</c>, <c>iat</c> and <c>exp</c> at most fifteen minutes apart. The private key comes from
/// <c>Cloud:Worker:SigningKeyPem</c>; it is loaded once, never logged and never leaves this class. A token authorises one connection and is not
/// renewed: every connect asks for a fresh one.
/// </summary>
internal sealed class WorkerTokenIssuer : IDisposable
{
    /// <summary>The audience claim every worker requires.</summary>
    public const string Audience = "autonogrammer-worker";

    private readonly ECDsa _key;
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _time;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Initializes a new instance of the <see cref="WorkerTokenIssuer"/> class.</summary>
    /// <param name="options">The cloud options holding the signing key.</param>
    /// <param name="time">The clock that stamps <c>iat</c> and <c>exp</c>.</param>
    /// <exception cref="InvalidOperationException">The signing key is missing or is not an EC P-256 private key, or the lifetime is outside (0, 15 minutes].</exception>
    public WorkerTokenIssuer(IOptions<CloudOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        var worker = options.Value.Worker;
        if (worker.TokenLifetime <= TimeSpan.Zero || worker.TokenLifetime > CloudOptions.WorkerSettings.MaximumTokenLifetime)
        {
            throw new InvalidOperationException("Cloud:Worker:TokenLifetime must be positive and at most 15 minutes.");
        }

        _key = ECDsa.Create();
        bool valid;
        try
        {
            _key.ImportFromPem(CloudOptionsValidator.NormalizePem(worker.SigningKeyPem));
            valid = _key.KeySize == 256 && _key.ExportParameters(includePrivateParameters: true).D is { Length: > 0 };
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            valid = false;
        }

        if (!valid)
        {
            _key.Dispose();
            throw new InvalidOperationException("Cloud:Worker:SigningKeyPem must be a PKCS#8 PEM private key on the P-256 curve.");
        }

        // One key object for the life of the issuer: the crypto provider cache is keyed by it, so a new key object per token would grow the cache.
        _credentials = new SigningCredentials(new ECDsaSecurityKey(_key), SecurityAlgorithms.EcdsaSha256);
        _issuer = worker.Issuer;
        _lifetime = worker.TokenLifetime;
        _time = time;
    }

    /// <summary>Gets when a token issued now would expire.</summary>
    /// <returns>The expiry instant.</returns>
    public DateTimeOffset NextExpiry() => _time.GetUtcNow() + _lifetime;

    /// <summary>Signs a token for one session.</summary>
    /// <param name="sessionId">The session identifier; the worker serves only this subject.</param>
    /// <returns>The compact JWT. It is a credential: never log it.</returns>
    public string Issue(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var now = _time.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Iss] = _issuer,
                [JwtRegisteredClaimNames.Aud] = Audience,
                [JwtRegisteredClaimNames.Sub] = sessionId,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [JwtRegisteredClaimNames.Iat] = now.ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Exp] = (now + _lifetime).ToUnixTimeSeconds(),
            },
            NotBefore = now.UtcDateTime,
            SigningCredentials = _credentials,
        };
        return _handler.CreateToken(descriptor);
    }

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();

    /// <inheritdoc />
    public override string ToString() => "WorkerTokenIssuer (key not shown)";
}
