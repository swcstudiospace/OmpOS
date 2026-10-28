namespace OmpOS.Web.Demo;

/// <summary>One frame of the Demo user-level event stream.</summary>
/// <param name="Id">The event identifier, or null for a frame sent without one (<c>resync</c>).</param>
/// <param name="Kind">The SSE event name.</param>
/// <param name="Json">The one-line JSON data.</param>
internal sealed record DemoUserFrame(long? Id, string Kind, string Json);
