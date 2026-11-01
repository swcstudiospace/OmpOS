namespace Autonogrammer.Accounts;

/// <summary>A session answer of <c>/verify</c> and <c>/token</c>. Unknown members are skipped, because the service adds fields.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="TokenType">The token type, normally <c>bearer</c>.</param>
/// <param name="ExpiresIn">The lifetime of the access token in seconds.</param>
/// <param name="ExpiresAt">The expiry as Unix seconds, when the service sends it.</param>
/// <param name="RefreshToken">The refresh token.</param>
/// <param name="User">The user the session belongs to.</param>
internal sealed record TokenResponse(string? AccessToken, string? TokenType, int ExpiresIn, long? ExpiresAt, string? RefreshToken, UserResponse? User);
