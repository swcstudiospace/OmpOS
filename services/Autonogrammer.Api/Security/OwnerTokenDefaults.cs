namespace Autonogrammer.Api.Security;

/// <summary>Names used by the owner-token authentication scheme.</summary>
internal static class OwnerTokenDefaults
{
    /// <summary>The authentication scheme name.</summary>
    public const string AuthenticationScheme = "OwnerToken";

    /// <summary>The authentication type of the owner principal.</summary>
    public const string AuthenticationType = "owner-token";

    /// <summary>The name of the policy scheme that is the default and forwards to the scheme of the configured mode.</summary>
    public const string PolicyScheme = "ApiAuth";

    /// <summary>The HttpContext item that marks a request whose owner-token failure was throttled.</summary>
    public const string ThrottledItem = "Autonogrammer.OwnerToken.RetryAfterSeconds";
}
