using System.Security.Claims;
using System.Security.Cryptography;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.Dispatch.Devices;

/// <summary>
/// Validates a device credential. The parameter set is copied from the worker validator (ES256 only, issuer, audience, <c>exp</c> and
/// <c>iat</c> required, lifetime cap, injected <see cref="TimeProvider"/>, header-only token). This type references no worker type.
/// A credential is also refused when its <c>jti</c> is not the one stored for that device.
/// </summary>
internal sealed class DeviceCredentialValidator : IDisposable
{
    private static readonly string[] AllowedAlgorithms = [SecurityAlgorithms.EcdsaSha256];

    private readonly DispatchOptions _options;
    private readonly TimeProvider _time;
    private readonly IDispatchStore _store;
    private readonly ECDsaSecurityKey _signingKey;
    private readonly ECDsa _key;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Initializes a new instance of the <see cref="DeviceCredentialValidator"/> class.</summary>
    /// <param name="options">The dispatch options.</param>
    /// <param name="time">The clock used for every lifetime check.</param>
    /// <param name="store">The store that holds the current <c>jti</c>.</param>
    public DeviceCredentialValidator(IOptions<DispatchOptions> options, TimeProvider time, IDispatchStore store)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(store);
        _options = options.Value;
        _time = time;
        _store = store;
        _key = ECDsa.Create();
        _key.ImportFromPem(ExportPublicPem(options.Value));
        _signingKey = new ECDsaSecurityKey(_key);
        Parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Credentials.Issuer,
            ValidateAudience = true,
            ValidAudience = DeviceCredentialIssuer.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = AllowedAlgorithms,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, token, _) => IsLifetimeAcceptable(notBefore, expires, token),
            ClockSkew = TimeSpan.Zero,
        };
    }

    /// <summary>Gets the parameters a bearer handler validates tokens with.</summary>
    public TokenValidationParameters Parameters { get; }

    /// <summary>Validates a compact JWT taken from the Authorization header.</summary>
    /// <param name="token">The compact JWT.</param>
    /// <param name="cancellationToken">Cancels the store read.</param>
    /// <returns>The device principal when validation succeeds; otherwise null.</returns>
    public async ValueTask<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        TokenValidationResult result;
        try
        {
            result = await _handler.ValidateTokenAsync(token, Parameters).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            return null;
        }

        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            return null;
        }

        var deviceId = result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var accountId = result.ClaimsIdentity.FindFirst(DeviceCredentialIssuer.OwnerClaim)?.Value;
        var jti = result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(jti))
        {
            return null;
        }

        var row = await _store.GetDeviceAsync(accountId, deviceId, cancellationToken).ConfigureAwait(false);
        if (row is null || row.RevokedAt is not null || !string.Equals(row.CredentialJti, jti, StringComparison.Ordinal))
        {
            return null;
        }

        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();

    private bool IsLifetimeAcceptable(DateTime? notBefore, DateTime? expires, SecurityToken securityToken)
    {
        if (expires is not { } expiry || securityToken is not JsonWebToken token)
        {
            return false;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var skew = _options.ClockSkew;
        var issuedAt = token.IssuedAt;
        return expiry >= now - skew
            && (notBefore is not { } start || start <= now + skew)
            && issuedAt != DateTime.MinValue
            && issuedAt <= now + skew
            && expiry - issuedAt <= _options.Credentials.Lifetime;
    }

    private static string ExportPublicPem(DispatchOptions options)
    {
        using var privateKey = ECDsa.Create();
        privateKey.ImportFromPem(Cloud.Options.CloudOptionsValidator.NormalizePem(options.Credentials.SigningKeyPem));
        return privateKey.ExportSubjectPublicKeyInfoPem();
    }
}
