using System.Net;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>
/// Resolves a host once, classifies every answer, and dials the validated address. A later resolver answer is ignored.
/// </summary>
public static class PinnedDial
{
    /// <summary>Selects and dials the pinned endpoint.</summary>
    /// <param name="host">The host name or literal.</param>
    /// <param name="port">The port.</param>
    /// <param name="resolver">The resolver. It is not called for a literal, and never more than once.</param>
    /// <param name="grants">The session local-origin grants.</param>
    /// <param name="options">The host policy.</param>
    /// <param name="connector">The connector. It receives an <see cref="IPEndPoint"/> and must not look the name up.</param>
    /// <param name="dispatchTurn">Whether Open must be ignored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connected stream, or null when the target is refused.</returns>
    public static async ValueTask<Stream?> ConnectAsync(
        string host,
        int port,
        IHostResolver resolver,
        OriginGrants grants,
        BrowserUseOptions options,
        ITcpConnector connector,
        bool dispatchTurn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connector);
        var endpoint = await SelectAsync(host, port, resolver, grants, options, dispatchTurn, cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
        {
            return null;
        }

        return await connector.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Selects the pinned endpoint without a second lookup.</summary>
    /// <param name="host">The host name or literal.</param>
    /// <param name="port">The port.</param>
    /// <param name="resolver">The resolver.</param>
    /// <param name="grants">The session grants.</param>
    /// <param name="options">The host policy.</param>
    /// <param name="dispatchTurn">Whether Open must be ignored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The endpoint, or null when refused.</returns>
    public static async ValueTask<IPEndPoint?> SelectAsync(
        string host,
        int port,
        IHostResolver resolver,
        OriginGrants grants,
        BrowserUseOptions options,
        bool dispatchTurn,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(options);
        if (port is < 1 or > 65535 || !BrowserUrlPolicy.IsHostListed(host, options, dispatchTurn))
        {
            return null;
        }

        var literal = AddressClassifier.ClassifyLiteral(host);
        if (!string.Equals(literal.Reason, "unparsed", StringComparison.Ordinal))
        {
            return SelectLiteral(host, port, grants, literal);
        }

        var answers = await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        var chosen = SelectName(host, port, grants, answers);
        return chosen is null ? null : new IPEndPoint(chosen, port);
    }

    private static IPEndPoint? SelectLiteral(string host, int port, OriginGrants grants, AddressClassification literal)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            return null;
        }

        if (!literal.IsRefused)
        {
            return new IPEndPoint(address, port);
        }

        if (grants.Contains(host, port) && IsGrantedLoopback(address))
        {
            return new IPEndPoint(address, port);
        }

        return null;
    }

    private static IPAddress? SelectName(string host, int port, OriginGrants grants, IReadOnlyList<IPAddress> answers)
    {
        if (grants.Contains(host, port))
        {
            return answers.FirstOrDefault(IsGrantedLoopback);
        }

        foreach (var answer in answers)
        {
            if (!AddressClassifier.Classify(answer).IsRefused)
            {
                return answer;
            }
        }

        return null;
    }

    private static bool IsGrantedLoopback(IPAddress address) =>
        IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Loopback);
}
