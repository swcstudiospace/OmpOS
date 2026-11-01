using Autonogrammer.Presentation.Export;

namespace Autonogrammer.Presentation.Usage;

/// <summary>
/// The usage history an export lists. One <see cref="UsageRecord"/> per ledger session and day, built by the host from the
/// ledger. An empty ledger returns an empty history, never an error.
/// </summary>
public interface IUsageHistorySource
{
    /// <summary>Reads the accumulated history.</summary>
    /// <returns>The history. Empty when nothing has been recorded.</returns>
    UsageHistory Read();
}
