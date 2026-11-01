using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Usage;

/// <summary>
/// One end-of-turn usage row. It holds the session, the UTC day, the context window and its occupancy, and the cost when
/// the engine reported one. It has no prompt text, arguments or result bytes.
/// </summary>
/// <param name="SessionId">The session the row belongs to. Not empty.</param>
/// <param name="Day">The UTC day the row was appended.</param>
/// <param name="Size">The context window in tokens.</param>
/// <param name="Used">The tokens the conversation occupied at this update. Occupancy, not a delta.</param>
/// <param name="Cost">The session-cumulative cost reported with this update, or null when the engine reported none.</param>
public sealed record UsageLedgerEntry(string SessionId, DateOnly Day, long Size, long Used, UsageCost? Cost)
{
    /// <summary>Returns the type name. Occupancy and cost stay out of logs.</summary>
    public override string ToString() => nameof(UsageLedgerEntry);
}
