using System.Security.Cryptography;
using Autonogrammer.Api.Cloud.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Signs the bearer token a worker requires on its WebSocket upgrade (ADR-0020 D4): ES256, <c>aud</c> the legacy worker audience (<c>LegacyBrandNames.WorkerAudience</c>), <c>iss</c> from
/// <c>Cloud:Worker:Issuer</c>, <c>sub</c> the session identifier, a unique <c>jti</c>, <c>iat</c> and <c>exp</c> at most fifteen minutes apart. The private key comes from
/// <c>Cloud:Worker:SigningKeyPem</c>; it is loaded once, never logged and never leaves this class. A token authorises one connection and is not
/// renewed: every connect asks for a fresh one.
/// </summary>
internal sealed class WorkerTokenIssuer : IDisposable
{
    /// <summary>The audience claim every worker requires.</summary>
    public const string Audience = LegacyBrandNames.WorkerAudience;

    /// <summary>The audience claim of a purge token. A worker accepts it only on <c>DELETE /accounts/{ownerDir}</c>.</summary>
    public const string PurgeAudience = "autonogrammer-worker-purge";

    /// <summary>The <c>purpose</c> claim a purge token carries. A session token has none.</summary>
    public const string PurgePurpose = "purge";

    /// <summary>How long a purge token lives.</summary>
    public static readonly TimeSpan PurgeLifetime = TimeSpan.FromMinutes(5);

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
    /// <param name="ownerDir">The account's workspace directory name: the lowercase hex SHA-256 of the owner id, carried as the <c>owner_dir</c> claim.</param>
    /// <returns>The compact JWT. It is a credential: never log it.</returns>
    public string Issue(string sessionId, string ownerDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerDir);
        return Sign(Audience, _lifetime, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JwtRegisteredClaimNames.Sub] = sessionId,
            ["owner_dir"] = ownerDir,
        });
    }

    /// <summary>Signs the token that asks a worker to purge one account's workspace (<c>DELETE /accounts/{ownerDir}</c>). It carries the purge audience, the purpose <c>purge</c>, the owner directory and a five-minute lifetime; a session token carries no purpose and does not authorise a purge.</summary>
    /// <param name="ownerDir">The account's workspace directory name, carried as the <c>owner_dir</c> claim.</param>
    /// <returns>The compact JWT. It is a credential: never log it.</returns>
    public string IssuePurge(string ownerDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerDir);
        return Sign(PurgeAudience, PurgeLifetime, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["purpose"] = PurgePurpose,
            ["owner_dir"] = ownerDir,
        });
    }

    private string Sign(string audience, TimeSpan lifetime, Dictionary<string, object> claims)
    {
        var now = _time.GetUtcNow();
        claims[JwtRegisteredClaimNames.Iss] = _issuer;
        claims[JwtRegisteredClaimNames.Aud] = audience;
        claims[JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N");
        claims[JwtRegisteredClaimNames.Iat] = now.ToUnixTimeSeconds();
        claims[JwtRegisteredClaimNames.Exp] = (now + lifetime).ToUnixTimeSeconds();
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
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
