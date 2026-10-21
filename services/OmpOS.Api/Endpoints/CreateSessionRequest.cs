namespace OmpOS.Api.Endpoints;

/// <summary>
/// Request body for <c>POST /sessions</c>. The owner defaults to the ambient principal when omitted.
/// </summary>
/// <remarks>
/// The real auth provider owns this field: the middleware stays the single decision point, and
/// this value is only honored while the host serves its single dev principal. At most 256
/// characters; longer values are rejected with 400.
/// </remarks>
internal sealed record CreateSessionRequest
{
    /// <summary>Gets the owning principal identifier, or null to use the ambient principal.</summary>
    public string? OwnerId { get; init; }
}
