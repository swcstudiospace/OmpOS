namespace OmpOS.Web.Services;

/// <summary>
/// The body sent to <c>POST /sessions</c>.
/// </summary>
internal sealed record CreateSessionRequest
{
    /// <summary>Gets the reserved owner identifier (single dev principal).</summary>
    public required string OwnerId { get; init; }
}
