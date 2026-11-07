namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// Timing of the email code.
/// </summary>
/// <param name="ExpirySeconds">How long a code stays valid.</param>
/// <param name="ResendCooldownSeconds">How long a person waits before asking for a new code.</param>
public sealed record AuthEmailCode(int ExpirySeconds, int ResendCooldownSeconds);
