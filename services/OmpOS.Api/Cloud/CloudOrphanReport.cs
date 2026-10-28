namespace OmpOS.Api.Cloud;

/// <summary>What an orphan scan found.</summary>
/// <param name="RecordsWithoutWorker">The agents whose recorded worker the provider no longer has; they were marked orphaned.</param>
/// <param name="WorkersWithoutRecord">The provider identifiers of labelled workers that no agent record claims.</param>
/// <param name="WorkersRemoved">The provider identifiers of orphan workers that were removed (only when cleanup is on).</param>
/// <param name="UnlabelledWorkers">How many workers the provider listed that carry no agent label. They are never removed.</param>
/// <param name="InventoryAvailable">Whether the provider can list workers; when false, workers without a record cannot be found.</param>
internal sealed record CloudOrphanReport(
    IReadOnlyList<string> RecordsWithoutWorker,
    IReadOnlyList<string> WorkersWithoutRecord,
    IReadOnlyList<string> WorkersRemoved,
    int UnlabelledWorkers,
    bool InventoryAvailable);
