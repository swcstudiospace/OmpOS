using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The data of a <c>session_status</c> event: the status of one session after it changed.</summary>
public sealed record SessionStatusEvent
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the stored session status.</summary>
    public required SessionStatus Status { get; init; }

    /// <summary>Gets the status the rail shows for the session.</summary>
    public required CatalogRowStatus RowStatus { get; init; }

    /// <summary>Gets the rail group of the session.</summary>
    public required CatalogRowGroup Group { get; init; }

    /// <summary>Gets the time of the last activity of the session.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }
}
