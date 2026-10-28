namespace OmpOS.Web.Services;

/// <summary>How the web head was composed.</summary>
/// <param name="IsDemo">True when the head runs on the in-browser Demo handler and no API exists.</param>
/// <param name="ApiHost">The host (and port) of the API for display, or <c>demo</c> in Demo mode. It carries no credential.</param>
internal sealed record WebWorkbenchOptions(bool IsDemo, string ApiHost);
