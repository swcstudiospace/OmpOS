using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts;

/// <summary>
/// PKCE material for the magic link flow: a random verifier, its S256 challenge, and the random nonce that names the loopback callback path.
/// </summary>
public static class Pkce
{
    /// <summary>The number of random bytes behind a verifier; the base64url text is 86 characters, inside the 43 to 128 of RFC 7636.</summary>
    public const int VerifierBytes = 64;

    /// <summary>The number of random bytes behind a nonce.</summary>
    public const int NonceBytes = 24;

    /// <summary>The challenge method name sent to the service.</summary>
    public const string ChallengeMethod = "s256";

    /// <summary>Creates a verifier from 64 random bytes, base64url encoded without padding.</summary>
    /// <returns>The verifier.</returns>
    public static SecretString CreateVerifier() => new(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(VerifierBytes)));

    /// <summary>Creates a callback nonce from random bytes, base64url encoded without padding. It is not secret from the person but must not be guessable.</summary>
    /// <returns>The nonce.</returns>
    public static string CreateNonce() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(NonceBytes));

    /// <summary>Computes the S256 challenge of a verifier: the base64url encoding, without padding, of the SHA-256 of its ASCII bytes.</summary>
    /// <param name="verifier">The verifier.</param>
    /// <returns>The challenge.</returns>
    public static string ComputeChallenge(SecretString verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier.Reveal())));
    }
}
