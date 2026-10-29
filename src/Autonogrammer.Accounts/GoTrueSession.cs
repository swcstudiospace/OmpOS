using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts;

/// <summary>
/// A signed-in session as the auth service issued it. Tokens are <see cref="SecretString"/> and never render.
/// </summary>
/// <param name="AccessToken">The short-lived bearer for the Autonogrammer API.</param>
/// <param name="RefreshToken">The single-use token that obtains the next session.</param>
/// <param name="ExpiresAt">When the access token stops being valid, by the local clock at the time the answer arrived.</param>
/// <param name="Lifetime">How long the access token lives from issue.</param>
/// <param name="UserId">The stable user identifier, the token <c>sub</c> claim.</param>
/// <param name="Email">The address of the user, or null when the service did not return one.</param>
public sealed record GoTrueSession(
    SecretString AccessToken,
    SecretString RefreshToken,
    DateTimeOffset ExpiresAt,
    TimeSpan Lifetime,
    string UserId,
    string? Email);
