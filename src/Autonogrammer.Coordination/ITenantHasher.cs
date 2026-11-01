namespace Autonogrammer.Coordination;

/// <summary>
/// Turns an account id into the opaque tenant segment of a coordination key. The segment is keyed, so a key or a log line
/// never reveals the account id and cannot be recomputed without the secret.
/// </summary>
public interface ITenantHasher
{
    /// <summary>Hashes an authenticated account id into its tenant segment.</summary>
    /// <param name="accountId">The authenticated subject. Any non-empty string; it is never treated as an email or a token.</param>
    /// <returns>32 lowercase hexadecimal characters: the first 16 bytes of HMAC-SHA256 over the account id.</returns>
    string HashTenant(string accountId);
}
