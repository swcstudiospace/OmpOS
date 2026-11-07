#pragma warning disable MA0048 // The plan owns this file for the store model.
namespace Autonogrammer.Cowork.Memory;

/// <summary>
/// One stored memory entry. <see cref="ToString"/> names the id only: content never appears in logs, exceptions or this text.
/// </summary>
public sealed class MemoryEntry
{
    /// <summary>Gets the entry id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the account id.</summary>
    public required string AccountId { get; init; }

    /// <summary>Gets the project id, or null for an account entry.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the topic.</summary>
    public string? Topic { get; init; }

    /// <summary>Gets the content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the category.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the session that saved the entry.</summary>
    public string? SourceSession { get; init; }

    /// <summary>Gets when the entry was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the entry was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Returns the type and id. Content, topic and category are omitted.</summary>
    /// <returns>A secret-free label.</returns>
    public override string ToString() => "MemoryEntry " + Id;
}

/// <summary>Who a session may read and write.</summary>
/// <param name="AccountId">The account.</param>
/// <param name="ProjectId">The project, or null when the session has none.</param>
/// <param name="UseAccountMemory">Whether a project session may also read account entries.</param>
/// <param name="SessionId">The source session. The hourly cap counts saves for this id.</param>
public sealed record MemoryScope(string AccountId, string? ProjectId, bool UseAccountMemory, string SessionId);

/// <summary>A proposed save. Policy runs on these fields before the row is written.</summary>
/// <param name="Topic">The topic.</param>
/// <param name="Content">The content.</param>
/// <param name="Category">The category, or null.</param>
public sealed record MemoryDraft(string? Topic, string Content, string? Category);

/// <summary>Account flags the store enforces. Enabled is recorded; pause and include-sensitive change writes.</summary>
/// <param name="Enabled">Whether the account has memory enabled.</param>
/// <param name="Paused">Whether reads and writes are refused.</param>
/// <param name="IncludeSensitive">Whether sensitive categories may be saved.</param>
public sealed record MemorySettings(bool Enabled, bool Paused, bool IncludeSensitive);

/// <summary>Stable codes for a refused or failed memory operation.</summary>
public static class MemoryCodes
{
    /// <summary>The write was stored.</summary>
    public const string Ok = "Ok";

    /// <summary>A never-saved class was refused.</summary>
    public const string MemoryRefusedNeverSave = "MemoryRefusedNeverSave";

    /// <summary>A sensitive class was refused because include_sensitive is off.</summary>
    public const string MemoryRefusedSensitive = "MemoryRefusedSensitive";

    /// <summary>The account is at the entry cap, or a field is over its cap.</summary>
    public const string MemoryCapReached = "MemoryCapReached";

    /// <summary>Memory is paused. Entries are kept.</summary>
    public const string MemoryPaused = "MemoryPaused";

    /// <summary>The session used its hourly save allowance.</summary>
    public const string MemoryRateLimited = "MemoryRateLimited";

    /// <summary>The query is longer than 200 characters.</summary>
    public const string MemoryQueryTooLong = "MemoryQueryTooLong";

    /// <summary>The entry is not in scope.</summary>
    public const string MemoryNotFound = "MemoryNotFound";

    /// <summary>No bearer was available.</summary>
    public const string SignedOut = "SignedOut";

    /// <summary>The memory API could not be reached.</summary>
    public const string Offline = "Offline";

    /// <summary>Memory is off for this task.</summary>
    public const string MemoryOffForTask = "MemoryOffForTask";

    /// <summary>The tool host cannot reach omp. The brief stays; saving is a user action.</summary>
    public const string ToolHostUnavailable = "ToolHostUnavailable";
}

/// <summary>The outcome of a write, delete or reset. <see cref="Sentence"/> never repeats the content.</summary>
/// <param name="Succeeded">Whether the operation completed.</param>
/// <param name="Code">A <see cref="MemoryCodes"/> value.</param>
/// <param name="Sentence">The sentence for the agent, or null on success.</param>
/// <param name="Entry">The written entry, or null.</param>
public sealed record MemoryMutation(bool Succeeded, string Code, string? Sentence, MemoryEntry? Entry);

/// <summary>A recall. On failure the list is empty and <see cref="Sentence"/> says why.</summary>
/// <param name="Succeeded">Whether entries were read.</param>
/// <param name="Code">A <see cref="MemoryCodes"/> value.</param>
/// <param name="Sentence">The sentence for the agent, or null on success.</param>
/// <param name="Entries">The entries in scope. Empty on failure.</param>
public sealed record MemoryRecall(bool Succeeded, string Code, string? Sentence, IReadOnlyList<MemoryEntry> Entries);

/// <summary>A notice raised when a write is refused. It carries the code and the sentence, never the content.</summary>
/// <param name="Code">A <see cref="MemoryCodes"/> value.</param>
/// <param name="Sentence">The sentence.</param>
public sealed record MemoryNotice(string Code, string Sentence);

/// <summary>Receives refusal notices. The store raises one before it returns a refusal.</summary>
public interface IMemoryNoticeSink
{
    /// <summary>Reports a notice.</summary>
    /// <param name="notice">The notice.</param>
    void Report(MemoryNotice notice);
}
