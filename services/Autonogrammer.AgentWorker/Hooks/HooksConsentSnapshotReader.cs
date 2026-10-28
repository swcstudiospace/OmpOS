using System.Security.Claims;
using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.AgentWorker.Hooks;

/// <summary>
/// Reads the consent snapshot of a connection from the <c>autonogrammer_hooks_consent</c> claim of its bearer token. The token is minted and signed by the
/// control plane, so the snapshot is as authentic as the connection itself: the worker has no other channel for it (it makes no outbound call and
/// takes no request body on <c>/acp</c>). The claim is a string holding a JSON object
/// <c>{"version":2,"approver":"…","approvedAt":"2026-10-28T09:00:00+00:00","hashes":["&lt;64 hex&gt;", …]}</c>. A claim that is present but not valid is
/// refused as a whole: the session then runs no project hook, exactly as if no snapshot were sent.
/// </summary>
internal static class HooksConsentSnapshotReader
{
    /// <summary>Gets the claim type that carries the snapshot.</summary>
    public const string ClaimType = "autonogrammer_hooks_consent";

    private const int MaxClaimBytes = 64 * 1024;
    private const int MaxApproverLength = 200;
    private const int HashLength = 64;

    /// <summary>Reads the snapshot.</summary>
    /// <param name="principal">The authenticated principal of the connection.</param>
    /// <param name="maxHashes">The most hashes a snapshot may name.</param>
    /// <returns>The snapshot, a refusal, or neither.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="principal"/> is null.</exception>
    public static HooksConsentRead Read(ClaimsPrincipal principal, int maxHashes)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var claims = principal.FindAll(ClaimType).ToArray();
        if (claims.Length == 0)
        {
            return HooksConsentRead.Absent;
        }

        if (claims.Length != 1)
        {
            return Refused("The consent claim appears more than once.");
        }

        return Parse(claims[0].Value, maxHashes);
    }

    /// <summary>Parses the text of the claim.</summary>
    /// <param name="json">The claim value.</param>
    /// <param name="maxHashes">The most hashes a snapshot may name.</param>
    /// <returns>The snapshot or a refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    public static HooksConsentRead Parse(string json, int maxHashes)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxClaimBytes)
        {
            return Refused("The consent claim is too large.");
        }

        HooksConsentSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize(json, WorkerHooksJsonContext.Default.HooksConsentSnapshot);
        }
        catch (JsonException)
        {
            return Refused("The consent claim is not a valid snapshot.");
        }

        if (snapshot is null)
        {
            return Refused("The consent claim is not a valid snapshot.");
        }

        if (snapshot.Version != HookConsentIdentity.CurrentVersion)
        {
            return Refused("The consent snapshot uses a hook identity version this worker does not run.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Approver) || snapshot.Approver.Length > MaxApproverLength || snapshot.Approver.Any(char.IsControl))
        {
            return Refused("The consent snapshot names no valid approver.");
        }

        if (snapshot.Hashes is null || snapshot.Hashes.Count > maxHashes || !snapshot.Hashes.All(IsHash))
        {
            return Refused("The consent snapshot holds an invalid or too long list of hook identities.");
        }

        return new HooksConsentRead(snapshot, null);
    }

    private static bool IsHash(string? value) =>
        value is { Length: HashLength } && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static HooksConsentRead Refused(string problem) => new(null, problem);
}
