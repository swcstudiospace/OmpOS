using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.Dispatch;

/// <summary>The hub's half of the hello: the challenge nonce, its hash for the single-use claim, and the check of the device's signature against the key it registered (D-14).</summary>
internal static class DispatchHandshake
{
    /// <summary>How long a challenge nonce stays claimable: ten minutes (D-14).</summary>
    public static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Creates a fresh 128-bit nonce, base64url.</summary>
    /// <returns>The nonce.</returns>
    public static string NewNonce() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16));

    /// <summary>Hashes a nonce for the store. The nonce itself is never stored or logged.</summary>
    /// <param name="nonce">The nonce.</param>
    /// <returns>The SHA-256 of the nonce in lowercase hexadecimal.</returns>
    public static string HashNonce(string nonce) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));

    /// <summary>Verifies the device's ES256 signature over the hello's canonical bytes against the public key it registered.</summary>
    /// <param name="publicKeyJson">The canonical JWK stored on the device row.</param>
    /// <param name="canonical">The canonical bytes the hub rebuilt.</param>
    /// <param name="signature">The raw P-1363 signature, base64url.</param>
    /// <returns><see langword="true"/> when the signature verifies; false for any malformed key or signature.</returns>
    public static bool VerifyHello(string publicKeyJson, byte[] canonical, string signature)
    {
        try
        {
            using var document = JsonDocument.Parse(publicKeyJson);
            var x = Base64UrlEncoder.DecodeBytes(document.RootElement.GetProperty("x").GetString());
            var y = Base64UrlEncoder.DecodeBytes(document.RootElement.GetProperty("y").GetString());
            var parameters = new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = x, Y = y } };
            using var ecdsa = ECDsa.Create(parameters);
            return ecdsa.VerifyData(canonical, Base64UrlEncoder.DecodeBytes(signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
