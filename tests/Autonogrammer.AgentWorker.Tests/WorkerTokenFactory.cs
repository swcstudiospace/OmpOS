using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>
/// Plays the control plane: holds a fresh ECDSA P-256 key pair, exposes the public half as the PEM a worker is configured with, and mints
/// worker tokens signed with the private half. Every claim is an explicit argument so a test can break exactly one rule.
/// </summary>
internal sealed class WorkerTokenFactory : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>Gets the public key as the PEM the worker's <c>Worker:Auth:PublicKeysPem</c> holds.</summary>
    public string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();

    /// <summary>Gets the private key as PEM. A worker must refuse it.</summary>
    public string PrivateKeyPem => _key.ExportPkcs8PrivateKeyPem();

    /// <summary>Mints a token signed with ES256.</summary>
    /// <param name="subject">The <c>sub</c> claim, the session id.</param>
    /// <param name="now">The instant the control plane thinks it is.</param>
    /// <param name="lifetime">Seconds between <c>iat</c> and <c>exp</c>. Defaults to 5 minutes.</param>
    /// <param name="issuedAgo">How long before <paramref name="now"/> the token was issued. Negative means issued in the future.</param>
    /// <param name="issuer">The <c>iss</c> claim; null omits it.</param>
    /// <param name="audience">The <c>aud</c> claim; null omits it.</param>
    /// <param name="includeIssuedAt">Whether to write <c>iat</c>.</param>
    /// <param name="includeExpiry">Whether to write <c>exp</c>.</param>
    /// <returns>The compact serialized token.</returns>
    public string Mint(
        string subject,
        DateTimeOffset now,
        TimeSpan? lifetime = null,
        TimeSpan? issuedAgo = null,
        string? issuer = "autonogrammer-api",
        string? audience = "autonogrammer-worker",
        bool includeIssuedAt = true,
        bool includeExpiry = true)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = BuildClaims(subject, now, lifetime, issuedAgo, issuer, audience, includeIssuedAt, includeExpiry),
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(_key), SecurityAlgorithms.EcdsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Mints an HS256 token whose HMAC key is the UTF-8 text of this factory's public key PEM: the classic algorithm confusion attack.</summary>
    /// <param name="subject">The <c>sub</c> claim.</param>
    /// <param name="now">The instant the control plane thinks it is.</param>
    /// <returns>The compact serialized token.</returns>
    public string MintHmacSignedWithPublicKey(string subject, DateTimeOffset now)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = BuildClaims(subject, now, null, null, "autonogrammer-api", "autonogrammer-worker", true, true),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PublicKeyPem)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();

    private static Dictionary<string, object> BuildClaims(
        string subject,
        DateTimeOffset now,
        TimeSpan? lifetime,
        TimeSpan? issuedAgo,
        string? issuer,
        string? audience,
        bool includeIssuedAt,
        bool includeExpiry)
    {
        var issuedAt = now - (issuedAgo ?? TimeSpan.Zero);
        var claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = subject };
        if (issuer is not null)
        {
            claims["iss"] = issuer;
        }

        if (audience is not null)
        {
            claims["aud"] = audience;
        }

        if (includeIssuedAt)
        {
            claims["iat"] = issuedAt.ToUnixTimeSeconds();
        }

        // The token handler stamps nbf with the real clock when the claim is absent, which would put every token in the future of the injected clock.
        claims["nbf"] = issuedAt.ToUnixTimeSeconds();
        if (includeExpiry)
        {
            claims["exp"] = (issuedAt + (lifetime ?? TimeSpan.FromMinutes(5))).ToUnixTimeSeconds();
        }

        return claims;
    }
}
