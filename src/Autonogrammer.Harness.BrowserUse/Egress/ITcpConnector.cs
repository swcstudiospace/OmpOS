using System.Net;
namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>Dials an already validated endpoint. Implementations must not resolve a name.</summary>
public interface ITcpConnector
{
    /// <summary>Connects to <paramref name="endpoint"/> with no name lookup.</summary>
    /// <param name="endpoint">The pinned endpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connected stream.</returns>
    ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}
