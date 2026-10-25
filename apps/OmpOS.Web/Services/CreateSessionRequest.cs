namespace OmpOS.Web.Services;

/// <summary>
/// The body sent to <c>POST /sessions</c>. It names no owner: the API takes the owner from the authenticated caller
/// and refuses a body that names one with 400 "Owner not accepted".
/// </summary>
internal sealed record CreateSessionRequest;
