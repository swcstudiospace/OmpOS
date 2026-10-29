namespace Autonogrammer.Accounts;

/// <summary>The only member of an error body this library reads. The message text is never read, kept or shown.</summary>
/// <param name="ErrorCode">The machine-readable error code, when the service sends one.</param>
internal sealed record ErrorResponse(string? ErrorCode);
