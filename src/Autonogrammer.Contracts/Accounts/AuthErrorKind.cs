namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// The closed catalogue of sign-in failures. Every failure of the GoTrue client, the session manager and the sign-in coordinator is exactly one
/// of these, and every one has fixed copy, so no raw exception text and no server text reaches a screen or a log. A loopback bind failure is a
/// notice, not a member of this list.
/// </summary>
public enum AuthErrorKind
{
    /// <summary>The shared hourly email limit of the project was reached (429 <c>over_email_send_rate_limit</c>).</summary>
    EmailRateLimited,

    /// <summary>The request limit was reached (429 <c>over_request_rate_limit</c> or any other 429, or a resend inside the cooldown); the wait comes from <c>Retry-After</c> or the cooldown when known.</summary>
    TooManyRequests,

    /// <summary>The code is wrong or expired (403 <c>otp_expired</c>); counts toward the five attempts.</summary>
    CodeInvalidOrExpired,

    /// <summary>The link is invalid or expired (callback error, <c>bad_code_verifier</c>, <c>flow_state_*</c>); the code still works.</summary>
    LinkInvalidOrExpired,

    /// <summary>The link returned to a tab that is not the one that asked (web head); use the code from the email.</summary>
    LinkOpenedInWrongTab,

    /// <summary>The email address was refused (<c>validation_failed</c>, <c>email_address_invalid</c>).</summary>
    EmailInvalid,

    /// <summary>The project does not accept new users (<c>signup_disabled</c>).</summary>
    SignUpsClosed,

    /// <summary>The project requires a CAPTCHA the client cannot solve (<c>captcha_failed</c>).</summary>
    CaptchaRequired,

    /// <summary>The user is blocked (<c>user_banned</c>).</summary>
    AccountBlocked,

    /// <summary>The sign-in method is switched off in the project (<c>otp_disabled</c>, <c>email_provider_disabled</c>, <c>feature_disabled</c>).</summary>
    MethodUnavailable,

    /// <summary>A stored session can no longer be refreshed (refresh token revoked, used or expired).</summary>
    SessionEnded,

    /// <summary>The service could not be reached: transport failure, timeout or DNS.</summary>
    NetworkUnavailable,

    /// <summary>The service answered 5xx or <c>hook_timeout</c>.</summary>
    ServiceUnavailable,

    /// <summary>The sign-in configuration is not available: <c>GET /auth/config</c> is unreachable or invalid, or no client is configured.</summary>
    ConfigUnavailable,

    /// <summary>The Autonogrammer API answered 401 after a valid sign-in; the head signs out locally.</summary>
    AccountRejected,

    /// <summary>Anything else; shown as one fixed sentence.</summary>
    Unexpected,
}
