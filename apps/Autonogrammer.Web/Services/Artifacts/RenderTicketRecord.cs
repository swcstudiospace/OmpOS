namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>A render ticket: the host-relative URL of the ticketed render document.</summary>
/// <param name="Url">The URL, which starts with <c>/cowork/artifact-render/</c>.</param>
internal sealed record RenderTicketRecord(string Url);
