namespace Autonogrammer.Api.Options;

/// <summary>How the API authenticates requests (<c>Api:Auth:Mode</c>).</summary>
internal enum ApiAuthMode
{
    /// <summary>OIDC bearer tokens verified against the configured authority.</summary>
    Oidc = 0,

    /// <summary>One operator's owner token, verified against configured SHA-256 digests.</summary>
    OwnerToken = 1,
}
