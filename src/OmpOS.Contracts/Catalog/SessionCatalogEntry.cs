using System.Text.Json.Serialization;

using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Contracts.Catalog;

/// <summary>
/// One row of <c>GET /sessions</c>: every field of the stored <see cref="Session"/> under its existing name, plus the catalog fields the web
/// rail and board need. The catalog fields are additive, so a client that reads the list as sessions keeps working. <see cref="Status"/> is the
/// stored session status; the richer rail status is <see cref="RowStatus"/>.
/// </summary>
public sealed record SessionCatalogEntry
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the owner identifier: the subject of the bearer token that created the session.</summary>
    public required string OwnerId { get; init; }

    /// <summary>Gets the creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the last-update timestamp of the stored session.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the stored session status.</summary>
    public required SessionStatus Status { get; init; }

    /// <summary>Gets the sandbox identifier, or null when the session runs without a sandbox.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SandboxId { get; init; }

    /// <summary>Gets the ordered chat messages.</summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>Gets the append-only stream-event log.</summary>
    public required IReadOnlyList<StreamEvent> Events { get; init; }

    /// <summary>Gets what the session is. Sessions started through this API are <see cref="CatalogSessionKind.Agent"/>.</summary>
    public required CatalogSessionKind Kind { get; init; }

    /// <summary>Gets the status the rail shows for the session.</summary>
    public required CatalogRowStatus RowStatus { get; init; }

    /// <summary>Gets the rail group of the session.</summary>
    public required CatalogRowGroup Group { get; init; }

    /// <summary>Gets the time of the last activity: the later of the session update and its last logged event.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }

    /// <summary>Gets the Graph identifier of the session's plan, or null when no stage source reported one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GraphId { get; init; }

    /// <summary>Gets a value indicating whether the operator closed the session.</summary>
    public required bool Closed { get; init; }

    /// <summary>Gets the name of the folder the session works in, or null when the API has no workspace. It is never a full path.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkspaceLabel { get; init; }
}
