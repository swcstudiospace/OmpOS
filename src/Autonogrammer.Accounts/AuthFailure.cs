using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Accounts;

/// <summary>
/// Why a call failed. Every member is safe to show or log: <see cref="ServerCode"/> is kept only when it belongs to the fixed vocabulary of
/// <see cref="AuthServerCodes"/>, and server message text is never kept.
/// </summary>
/// <param name="Kind">The closed failure kind.</param>
/// <param name="RetryAfter">How long to wait before trying again, when the service said so or the coordinator knows; otherwise null.</param>
/// <param name="HttpStatus">The HTTP status the service answered, or null when there was no answer.</param>
/// <param name="ServerCode">The service error code from the fixed vocabulary, or null.</param>
public sealed record AuthFailure(AuthErrorKind Kind, TimeSpan? RetryAfter = null, int? HttpStatus = null, string? ServerCode = null);
