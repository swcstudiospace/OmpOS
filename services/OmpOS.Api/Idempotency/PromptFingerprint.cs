using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OmpOS.Api.Endpoints;
using OmpOS.Api.Serialization;

namespace OmpOS.Api.Idempotency;

/// <summary>
/// Fingerprints a prompt request for idempotent replay: the lowercase hex SHA-256 of the canonical JSON of the parsed DTO,
/// serialized through the source-generated <see cref="ApiJsonContext"/> (no reflection).
/// </summary>
internal static class PromptFingerprint
{
    /// <summary>Computes the fingerprint of a parsed prompt request.</summary>
    /// <param name="request">The parsed request body.</param>
    /// <returns>The lowercase hex SHA-256 digest.</returns>
    public static string Of(PromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = JsonSerializer.Serialize(request, ApiJsonContext.Default.PromptRequest);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
