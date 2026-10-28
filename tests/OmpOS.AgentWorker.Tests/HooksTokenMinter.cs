using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OmpOS.AgentWorker.Tests;

/// <summary>Plays the control plane for the hooks tests: mints a worker token signed with the key of a <see cref="WorkerTokenFactory"/> that carries the consent snapshot claim.</summary>
internal static class HooksTokenMinter
{
    /// <summary>The claim the worker reads the consent snapshot from.</summary>
    public const string ConsentClaim = "ompos_hooks_consent";

    /// <summary>Mints a valid worker token with an optional consent claim.</summary>
    /// <param name="tokens">The control plane whose key signs the token.</param>
    /// <param name="subject">The session id.</param>
    /// <param name="now">The instant the control plane thinks it is.</param>
    /// <param name="consent">The text of the consent claim, or null for a token without one.</param>
    /// <returns>The compact serialized token.</returns>
    public static string Mint(WorkerTokenFactory tokens, string subject, DateTimeOffset now, string? consent)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        using var key = ECDsa.Create();
        key.ImportFromPem(tokens.PrivateKeyPem);
        var seconds = now.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = subject,
            ["iss"] = "ompos-api",
            ["aud"] = "ompos-worker",
            ["iat"] = seconds,
            ["nbf"] = seconds,
            ["exp"] = seconds + 300,
        };
        if (consent is not null)
        {
            claims[ConsentClaim] = consent;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Builds the text of a version 2 consent snapshot.</summary>
    /// <param name="hashes">The approved hook identities.</param>
    /// <returns>The JSON text of the claim.</returns>
    public static string Snapshot(params string[] hashes) =>
        "{\"version\":2,\"approver\":\"ada@example.test\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":["
        + string.Join(',', hashes.Select(static hash => "\"" + hash + "\""))
        + "]}";
}
