namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// The caller's own sessions across surfaces, answered by <c>GET /account/sessions</c>, newest first.
/// </summary>
/// <param name="Items">The rows.</param>
public sealed record AccountSessionList(IReadOnlyList<AccountSessionItem> Items);
