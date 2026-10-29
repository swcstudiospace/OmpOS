namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// The caller's own account, answered by <c>GET /account</c>.
/// </summary>
/// <param name="Id">The stable owner identifier: the token <c>sub</c> claim, or <c>owner</c> for the single operator of owner-token mode.</param>
/// <param name="Email">The address from the validated token's <c>email</c> claim, or null when the token has none. It is never stored.</param>
/// <param name="CreatedAt">When the account row was created, or null for callers without an account row (owner-token and development callers).</param>
/// <param name="LastSeenAt">When the account was last seen, or null for callers without an account row.</param>
/// <param name="Features">The module keys that registered an account feature, such as <c>cowork</c> or <c>dispatch</c>, so a menu shows only what exists.</param>
public sealed record AccountResponse(
    string Id,
    string? Email,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSeenAt,
    IReadOnlyList<string> Features);
