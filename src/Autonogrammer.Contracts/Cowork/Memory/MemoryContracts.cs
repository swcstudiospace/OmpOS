#pragma warning disable MA0048 // The plan owns this file for the memory wire records.
namespace Autonogrammer.Contracts.Cowork.Memory;

/// <summary>One memory entry on the wire. The account id is not a member: the token owner is the account.</summary>
public sealed record MemoryEntryRecord
{
    /// <summary>Gets the entry id.</summary>
    public required string Id { get; init; }

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
}

/// <summary>A save. A body that names an account is refused before send.</summary>
public sealed record MemorySaveRequest
{
    /// <summary>Gets the topic.</summary>
    public string? Topic { get; init; }

    /// <summary>Gets the content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the category.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the project id, or null for an account entry.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the source session.</summary>
    public string? SourceSession { get; init; }

    /// <summary>Gets whether a project session may also read account entries. Stored with the call, not the row.</summary>
    public bool UseAccountMemory { get; init; }
}

/// <summary>A recall. Query is at most 200 characters.</summary>
public sealed record MemoryRecallRequest
{
    /// <summary>Gets the query. Empty returns the newest entries in scope.</summary>
    public string? Query { get; init; }

    /// <summary>Gets how many entries to return.</summary>
    public int K { get; init; }

    /// <summary>Gets the project id, or null for a session without a project.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets whether account entries are in scope for a project session.</summary>
    public bool UseAccountMemory { get; init; }
}

/// <summary>The recall response.</summary>
public sealed record MemoryRecallResponse
{
    /// <summary>Gets the matching entries.</summary>
    public IReadOnlyList<MemoryEntryRecord> Entries { get; init; } = [];
}

/// <summary>Account memory settings. Defaults are off so a missing flag never switches memory on.</summary>
public sealed record MemorySettingsRecord
{
    /// <summary>Gets whether memory is enabled for the account.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets whether memory is paused. Entries stay; reads and writes do not.</summary>
    public bool Paused { get; init; }

    /// <summary>Gets whether sensitive categories may be saved.</summary>
    public bool IncludeSensitive { get; init; }
}

/// <summary>A JSON export of the account's memory, including project memory.</summary>
public sealed record MemoryExportDocument
{
    /// <summary>Gets the entries.</summary>
    public IReadOnlyList<MemoryEntryRecord> Entries { get; init; } = [];
}

/// <summary>A typed refusal or store failure. The sentence is for the agent. The content is never repeated.</summary>
public sealed record MemoryErrorRecord
{
    /// <summary>Gets the stable code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the sentence shown to the agent.</summary>
    public required string Sentence { get; init; }
}
