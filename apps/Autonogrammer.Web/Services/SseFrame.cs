namespace Autonogrammer.Web.Services;

/// <summary>
/// One parsed server-sent event: the optional <c>id:</c>, <c>event:</c>, and <c>data:</c> fields.
/// On the Autonogrammer wire, <c>id</c> is the <c>StreamEvent.Seq</c>, <c>event</c> is the payload
/// kind (<c>text_delta</c>, <c>ui_block</c>, <c>error</c>, <c>completed</c>), and <c>data</c>
/// is the <c>StreamEvent</c> JSON.
/// </summary>
/// <param name="Id">The event id (<c>StreamEvent.Seq</c>), or null when the frame carries none.</param>
/// <param name="EventType">The event type (payload kind), or null when the frame carries none.</param>
/// <param name="Data">The data payload with multi-line data joined by newline.</param>
internal sealed record SseFrame(string? Id, string? EventType, string Data);
