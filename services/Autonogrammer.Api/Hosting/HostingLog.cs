namespace Autonogrammer.Api.Hosting;

/// <summary>Log messages of the hosting start-up checks.</summary>
internal static partial class HostingLog
{
    /// <summary>Warns that proxy headers are ignored outside Development, which is almost never what a hosted deployment wants.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="key">The configuration key of the trust mode.</param>
    [LoggerMessage(
        EventId = 120,
        Level = LogLevel.Warning,
        Message = "{Key} is None, so forwarded headers are ignored. Behind a reverse proxy or load balancer (Railway, a CDN) every request then appears to come from the proxy: the failed-token throttle and the per-address rate limits are shared by all clients (one client can lock the operator out of sign-in) and HSTS is never sent. Set TrustAllSingleHop for one trusted proxy, or KnownNetworks with the proxy's address range. Keep None only when clients connect directly.")]
    internal static partial void ForwardedHeadersIgnored(ILogger logger, string key);
}
