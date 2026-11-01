namespace Autonogrammer.Accounts;

/// <summary>
/// The identity <c>GET /user</c> answers. Only tests and the live check read it.
/// </summary>
/// <param name="UserId">The stable user identifier.</param>
/// <param name="Email">The address of the user, or null.</param>
public sealed record GoTrueUser(string UserId, string? Email);
