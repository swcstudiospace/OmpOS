using ModelContextProtocol.Client;

namespace OmpOS.Harness.Mcp;

/// <summary>Opens MCP client connections over the SDK's stdio and Streamable HTTP transports.</summary>
public static class McpClientConnector
{
    /// <summary>
    /// The MCP protocol revision every harness connection requests. It is the latest revision that still uses
    /// the <c>initialize</c> handshake and Streamable HTTP sessions. Pinning it disables the SDK's default
    /// <c>server/discover</c> probe for the newest revision, whose timeout-and-fallback path races under load.
    /// </summary>
    public const string ProtocolVersion = "2025-11-25";

    /// <summary>Launches a stdio MCP server and completes the MCP initialization handshake.</summary>
    /// <param name="options">How to launch the server.</param>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>A connected client; register it with <see cref="McpToolRegistry.RegisterServerAsync"/>.</returns>
    public static async Task<McpClient> ConnectStdioAsync(McpStdioServerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Command))
        {
            throw new ArgumentException("The stdio server command must be set.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            throw new ArgumentException("The stdio server working directory must be set.", nameof(options));
        }

        ArgumentNullException.ThrowIfNull(options.Arguments, nameof(options));

        var environment = options.EnvironmentVariables is null
            ? null
            : new Dictionary<string, string?>(options.EnvironmentVariables, StringComparer.Ordinal);

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = options.Command,
            Arguments = options.Arguments.ToList(),
            WorkingDirectory = options.WorkingDirectory,
            EnvironmentVariables = environment,
        });

        return await McpClient.CreateAsync(transport, PinnedOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static McpClientOptions PinnedOptions() => new() { ProtocolVersion = ProtocolVersion };

    /// <summary>Connects to a Streamable HTTP MCP server and completes the MCP initialization handshake.</summary>
    /// <param name="endpoint">The absolute http or https URL of the server's MCP endpoint.</param>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>A connected client; register it with <see cref="McpToolRegistry.RegisterServerAsync"/>.</returns>
    public static async Task<McpClient> ConnectHttpAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The MCP endpoint must be an absolute http or https URI.", nameof(endpoint));
        }

        var transport = CreateStreamableHttpTransport(endpoint);
        try
        {
            return await McpClient.CreateAsync(transport, PinnedOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (transport is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>Builds the Streamable HTTP transport; ownership passes to the caller.</summary>
    /// <param name="endpoint">The absolute MCP endpoint URI.</param>
    /// <returns>An unconnected transport.</returns>
    internal static IClientTransport CreateStreamableHttpTransport(Uri endpoint) =>
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
        });
}
