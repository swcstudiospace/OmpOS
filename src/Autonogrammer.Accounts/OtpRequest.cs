namespace Autonogrammer.Accounts;

/// <summary>Body of <c>POST /otp</c>.</summary>
/// <param name="Email">The address to send the code to.</param>
/// <param name="CreateUser">True so an unknown address becomes a user.</param>
/// <param name="CodeChallenge">The S256 challenge when the link flow is on, otherwise null.</param>
/// <param name="CodeChallengeMethod">The challenge method when the link flow is on, otherwise null.</param>
internal sealed record OtpRequest(string Email, bool CreateUser, string? CodeChallenge, string? CodeChallengeMethod);
