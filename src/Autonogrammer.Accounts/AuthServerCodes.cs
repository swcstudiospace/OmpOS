using System.Collections.Frozen;

namespace Autonogrammer.Accounts;

/// <summary>
/// The fixed vocabulary of service error codes a failure may carry. A code outside it is dropped, so a hostile or unexpected response body cannot put
/// text into a log or a message.
/// </summary>
public static class AuthServerCodes
{
    private static readonly FrozenSet<string> Known = new[]
    {
        "otp_expired",
        "otp_disabled",
        "over_email_send_rate_limit",
        "over_request_rate_limit",
        "signup_disabled",
        "user_banned",
        "captcha_failed",
        "refresh_token_not_found",
        "refresh_token_already_used",
        "session_not_found",
        "session_expired",
        "bad_code_verifier",
        "flow_state_not_found",
        "flow_state_expired",
        "email_address_invalid",
        "validation_failed",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the codes a failure may carry.</summary>
    public static IReadOnlyCollection<string> Vocabulary => Known;

    /// <summary>Returns a code when it is in the vocabulary.</summary>
    /// <param name="code">A code from a response body, or null.</param>
    /// <returns>The same code, or null when it is null or not in the vocabulary.</returns>
    public static string? Normalize(string? code) => code is not null && Known.TryGetValue(code, out string? known) ? known : null;
}
