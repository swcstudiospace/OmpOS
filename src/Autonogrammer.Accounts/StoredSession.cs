namespace Autonogrammer.Accounts;

/// <summary>The stored form of a session, written to the secret store. The access token is never stored.</summary>
/// <param name="Schema">The shape version, currently 1.</param>
/// <param name="Issuer">The token issuer of the project the session belongs to.</param>
/// <param name="RefreshToken">The refresh token.</param>
/// <param name="UserId">The user identifier.</param>
/// <param name="Email">The address, or null.</param>
internal sealed record StoredSession(int Schema, string Issuer, string RefreshToken, string UserId, string? Email);
