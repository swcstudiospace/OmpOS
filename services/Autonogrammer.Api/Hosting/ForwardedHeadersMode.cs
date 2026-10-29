namespace Autonogrammer.Api.Hosting;

/// <summary>How the host treats <c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c> and <c>X-Forwarded-Host</c>.</summary>
internal enum ForwardedHeadersMode
{
    /// <summary>The headers are ignored. The client address is the direct peer.</summary>
    None = 0,

    /// <summary>
    /// Exactly one hop of the headers is honoured from any sender. Correct only behind a platform edge that always
    /// overwrites the headers and is the only way to reach the process (for example Railway's edge proxy).
    /// </summary>
    TrustAllSingleHop = 1,

    /// <summary>Exactly one hop of the headers is honoured, and only from peers inside <see cref="HostingForwardedHeadersOptions.KnownNetworks"/>.</summary>
    KnownNetworks = 2,
}
