using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Web.Services;

/// <summary>
/// One event of the user-level stream (<c>GET /events</c>). <see cref="Kind"/> is the SSE event name (<see cref="CatalogEventKinds"/>); exactly
/// the member that belongs to the kind is set.
/// </summary>
/// <param name="Kind">The SSE event name.</param>
/// <param name="Id">The event identifier, or null for an event the API sent without one (<c>resync</c>).</param>
/// <param name="Catalog">The payload of <c>catalog_changed</c>.</param>
/// <param name="Status">The payload of <c>session_status</c>.</param>
/// <param name="Stage">The payload of <c>stage</c>.</param>
/// <param name="Resync">The payload of <c>resync</c>: the client must reload <c>GET /sessions</c>.</param>
internal sealed record UserStreamEvent(
    string Kind,
    long? Id,
    CatalogChangedEvent? Catalog,
    SessionStatusEvent? Status,
    StageEvent? Stage,
    CatalogResyncEvent? Resync);
