namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>One email the fake project "sent".</summary>
/// <param name="Email">The address.</param>
/// <param name="Kind">The template: <c>signup</c> for a first-time user, <c>magiclink</c> for a confirmed one.</param>
/// <param name="Code">The one-time code in the email, or null when the template shows only a link.</param>
/// <param name="LinkAuthCode">The code the link carries back to the redirect address, or null when no PKCE challenge was sent.</param>
/// <param name="RedirectTo">The redirect address the request named, or null.</param>
internal sealed record SentEmail(string Email, string Kind, string? Code, string? LinkAuthCode, string? RedirectTo);
