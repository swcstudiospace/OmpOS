namespace Autonogrammer.Accounts;

/// <summary>Body of <c>POST /token?grant_type=pkce</c>.</summary>
/// <param name="AuthCode">The code from the link callback.</param>
/// <param name="CodeVerifier">The verifier whose challenge was sent with the request for the link.</param>
internal sealed record PkceTokenRequest(string AuthCode, string CodeVerifier);
