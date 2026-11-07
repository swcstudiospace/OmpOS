#pragma warning disable MA0048 // MemoryExport is the export result of this contract.
namespace Autonogrammer.Cowork.Memory;

/// <summary>
/// The one memory contract. Every implementation runs <see cref="MemoryContentPolicy"/> before a write,
/// filters by account, and never logs content.
/// </summary>
public interface IMemoryStore
{
    /// <summary>Reads account settings. A missing row is off, not paused, and not sensitive.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The settings.</returns>
    Task<MemorySettings> GetSettingsAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Replaces account settings.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the settings are stored.</returns>
    Task SetSettingsAsync(string accountId, MemorySettings settings, CancellationToken cancellationToken);

    /// <summary>Saves one entry after the policy and the caps. A refusal raises a notice and writes nothing.</summary>
    /// <param name="scope">Who may write.</param>
    /// <param name="draft">The proposed text.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mutation.</returns>
    Task<MemoryMutation> SaveAsync(MemoryScope scope, MemoryDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// Reads entries in scope. A project session reads that project's entries, and account entries only when
    /// <see cref="MemoryScope.UseAccountMemory"/> is set. A session without a project reads account entries.
    /// </summary>
    /// <param name="scope">Who may read.</param>
    /// <param name="query">The query, at most <see cref="MemoryOptions.MaxQueryChars"/> characters. Empty returns the newest.</param>
    /// <param name="k">How many entries to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The recall.</returns>
    Task<MemoryRecall> RecallAsync(MemoryScope scope, string? query, int k, CancellationToken cancellationToken);

    /// <summary>Deletes one entry in scope. Other accounts and other projects are not found.</summary>
    /// <param name="scope">Who may delete.</param>
    /// <param name="id">The entry id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mutation.</returns>
    Task<MemoryMutation> DeleteAsync(MemoryScope scope, string id, CancellationToken cancellationToken);

    /// <summary>Hard-deletes every entry for the account, including project memory. Settings are kept.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mutation.</returns>
    Task<MemoryMutation> ResetAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Exports every entry for the account as JSON. Content is in the document, never in a log.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The JSON document, or a failure sentence.</returns>
    Task<MemoryExport> ExportAsync(string accountId, CancellationToken cancellationToken);
}

/// <summary>A JSON export, or a failure that does not include content.</summary>
/// <param name="Succeeded">Whether the document was built.</param>
/// <param name="Code">A <see cref="MemoryCodes"/> value.</param>
/// <param name="Sentence">The sentence, or null on success.</param>
/// <param name="Json">The document, or null.</param>
public sealed record MemoryExport(bool Succeeded, string Code, string? Sentence, string? Json);
