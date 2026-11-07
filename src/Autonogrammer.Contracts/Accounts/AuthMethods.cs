namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// Which sign-in methods the project offers.
/// </summary>
/// <param name="EmailCode">True when a one-time code sent by email can be entered.</param>
/// <param name="MagicLink">True when the PKCE magic link flow is on (needs owner action O1); false means code only.</param>
/// <param name="Providers">The OAuth providers the owner enabled in the identity project, by name: <c>google</c> and <c>x</c>. Empty when none. The project's settings do not report <c>x</c>, so the operator declares them.</param>
/// <param name="Wallet">True when Ethereum wallet sign-in is enabled in the identity project. The settings do not report it, so the operator declares it.</param>
public sealed record AuthMethods(bool EmailCode, bool MagicLink, IReadOnlyList<string> Providers, bool Wallet);
