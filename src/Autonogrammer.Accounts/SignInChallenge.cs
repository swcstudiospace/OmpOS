namespace Autonogrammer.Accounts;

/// <summary>
/// What a head shows while a code is awaited.
/// </summary>
/// <param name="Email">The address the code was sent to.</param>
/// <param name="SentAt">When the last email was requested.</param>
/// <param name="ExpiresAt">When the code stops being valid, from <see cref="GoTrueOptions.EmailCodeExpiry"/>.</param>
/// <param name="ResendAvailableAt">When another code may be requested, from <see cref="GoTrueOptions.ResendCooldown"/>.</param>
/// <param name="LinkAvailable">True when the email also carries a magic link that returns to the loopback listener.</param>
/// <param name="LinkNonce">The nonce in the link's callback path, or null when no link was requested.</param>
/// <param name="ShowFirstTimeHint">True when a first-time user may get an email without a code: the person opens its link once and then requests a new code.</param>
public sealed record SignInChallenge(
    string Email,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAvailableAt,
    bool LinkAvailable,
    string? LinkNonce,
    bool ShowFirstTimeHint);
