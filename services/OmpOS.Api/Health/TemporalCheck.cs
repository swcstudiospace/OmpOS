using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;

namespace OmpOS.Api.Health;

/// <summary>
/// TCP reachability probe for <c>/health/ready</c>: disabled reports healthy, enabled-without-address
/// reports degraded so misconfiguration surfaces in readiness, and an enabled endpoint is dialed with a
/// bounded timeout. This check proves only TCP reachability of the configured endpoint, not Temporal
/// workflow-service health.
/// </summary>
internal sealed class TemporalCheck : IHealthCheck
{
    private const int DefaultTemporalPort = 7233;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    private readonly IOptions<WorkflowsOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="TemporalCheck"/> class.</summary>
    /// <param name="options">The workflows options.</param>
    public TemporalCheck(IOptions<WorkflowsOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = _options.Value;
        if (!options.Enabled)
        {
            return HealthCheckResult.Healthy("Durable workflows are disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Address))
        {
            return HealthCheckResult.Degraded("Workflows:Enabled is true but no Workflows:Address is configured.");
        }

        var endpoint = options.Address.Trim();
        if (!TryParseEndpoint(endpoint, out var host, out var port))
        {
            return HealthCheckResult.Unhealthy($"Workflows:Address '{endpoint}' is not a parsable host:port or URI.");
        }

#pragma warning disable CA1031 // A readiness probe must report, never throw; any dial failure is the signal.
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"TCP connection to '{endpoint}' succeeded.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"TCP connection to '{endpoint}' failed ({exception.GetType().Name}).");
        }
#pragma warning restore CA1031
    }

    /// <summary>Parses a URI-with-scheme or bare host:port endpoint into host and port.</summary>
    /// <param name="endpoint">The trimmed configured endpoint.</param>
    /// <param name="host">The parsed host name.</param>
    /// <param name="port">The parsed port, or 7233 when none is given.</param>
    /// <returns>True when the endpoint parsed; otherwise false.</returns>
    private static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = DefaultTemporalPort;

        if (endpoint.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            host = uri.Host;
            port = !uri.IsDefaultPort || endpoint.Contains(":" + uri.Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                ? uri.Port
                : DefaultTemporalPort;
            return true;
        }

        var separator = endpoint.LastIndexOf(':');
        if (separator < 0)
        {
            host = endpoint;
            return !string.IsNullOrWhiteSpace(host);
        }

        host = endpoint[..separator];
        if (string.IsNullOrWhiteSpace(host)
            || !int.TryParse(endpoint[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed is < 1 or > 65535)
        {
            return false;
        }

        port = parsed;
        return true;
    }
}
