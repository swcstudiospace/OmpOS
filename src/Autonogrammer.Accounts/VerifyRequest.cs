namespace Autonogrammer.Accounts;

/// <summary>Body of <c>POST /verify</c>.</summary>
/// <param name="Type">The verification type, always <c>email</c>.</param>
/// <param name="Email">The address the code was sent to.</param>
/// <param name="Token">The code the person typed, normalised.</param>
internal sealed record VerifyRequest(string Type, string Email, string Token);
