namespace OmpOS.Api.Endpoints;

/// <summary>
/// Request body for <c>POST /sessions</c>. The owner is never taken from the body; it is the caller's subject.
/// </summary>
/// <remarks>
/// The owner is the subject of the bearer token that created the session, so a request cannot name another owner. A non-null
/// <c>ownerId</c> is refused with 400; the property exists only so that such a request is detected.
/// </remarks>
internal sealed record CreateSessionRequest
{
    /// <summary>Gets a body owner identifier, which is refused with 400 when non-null.</summary>
    public string? OwnerId { get; init; }
}
