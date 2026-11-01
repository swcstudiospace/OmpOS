namespace Autonogrammer.Accounts;

/// <summary>Who is signed in.</summary>
/// <param name="UserId">The stable user identifier, the token <c>sub</c> claim.</param>
/// <param name="Email">The address, or null when the service returned none.</param>
public sealed record AccountIdentity(string UserId, string? Email);
