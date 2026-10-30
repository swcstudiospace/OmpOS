namespace Autonogrammer.Api.Services;

/// <summary>One event of the user-level stream, ready to write: its identifier, its SSE event name and its JSON data.</summary>
/// <param name="Id">The sequence identifier, or 0 for a resync notice that is not part of the history.</param>
/// <param name="OwnerId">The owner the event is for.</param>
/// <param name="Kind">The SSE event name.</param>
/// <param name="Json">The JSON data.</param>
internal sealed record CatalogEventRecord(long Id, string OwnerId, string Kind, string Json);
