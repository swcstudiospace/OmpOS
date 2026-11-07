namespace Autonogrammer.Accounts;

/// <summary>The <c>gotrue_meta_security</c> object of a request body.</summary>
/// <param name="CaptchaToken">The CAPTCHA token the person solved.</param>
internal sealed record GoTrueSecurity(string CaptchaToken);
