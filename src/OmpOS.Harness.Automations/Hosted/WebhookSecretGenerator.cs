using System.Buffers.Text;
using System.Security.Cryptography;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Creates webhook signing secrets.
/// </summary>
public static class WebhookSecretGenerator
{
    private const int SecretBytes = 32;

    /// <summary>Creates a secret from 32 random bytes.</summary>
    /// <returns><c>whsec_</c> followed by the base64url text of the bytes.</returns>
    public static string Generate() => "whsec_" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>Creates an unguessable automation identifier from 16 random bytes.</summary>
    /// <returns><c>aut_</c> followed by 32 lower-case hex digits.</returns>
    public static string GenerateAutomationId() => "aut_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
