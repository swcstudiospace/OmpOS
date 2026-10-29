using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// One row of the account's session list.
/// </summary>
/// <param name="Session">The list-row view of the session.</param>
/// <param name="Surface">The label of the surface the session belongs to, <c>code</c> or <c>cowork</c>. A string because the session surface type arrives with the data plane phase.</param>
public sealed record AccountSessionItem(SessionSummary Session, string Surface);
