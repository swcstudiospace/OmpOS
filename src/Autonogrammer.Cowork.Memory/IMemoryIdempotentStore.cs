namespace Autonogrammer.Cowork.Memory;

/// <summary>Durable keyed memory creation for an authenticated account.</summary>
public interface IMemoryIdempotentStore
{
    /// <summary>Saves or replays a validated request. A key is scoped to the account and create operation.</summary>
    /// <param name="scope">Authenticated account and effective session scope.</param>
    /// <param name="draft">Validated semantic fields.</param>
    /// <param name="key">Validated 1–128 character visible ASCII key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The durable outcome; unavailable requires retrying with the same key.</returns>
    Task<MemoryIdempotentResult> SaveIdempotentAsync(MemoryScope scope, MemoryDraft draft, string key, CancellationToken cancellationToken);
}
