namespace Autonogrammer.Accounts;

/// <summary>Body of <c>POST /token?grant_type=refresh_token</c>.</summary>
/// <param name="RefreshToken">The refresh token to spend.</param>
internal sealed record RefreshTokenRequest(string RefreshToken);
