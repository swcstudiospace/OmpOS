namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// Which sign-in methods the project offers.
/// </summary>
/// <param name="EmailCode">True when a one-time code sent by email can be entered.</param>
/// <param name="MagicLink">True when the PKCE magic link flow is on (needs owner action O1); false means code only.</param>
public sealed record AuthMethods(bool EmailCode, bool MagicLink);
