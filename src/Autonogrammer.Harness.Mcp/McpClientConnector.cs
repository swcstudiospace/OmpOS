using Autonogrammer.Harness.Acp;
using ModelContextProtocol.Client;

namespace Autonogrammer.Harness.Mcp;

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

        var environment = BuildEnvironment(options, CurrentEnvironment);

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = options.Command,
            Arguments = options.Arguments.ToList(),
            WorkingDirectory = options.WorkingDirectory,
            EnvironmentVariables = environment,
            InheritEnvironmentVariables = options.EnvironmentMode == EngineEnvironmentMode.Inherit,
        });

        return await McpClient.CreateAsync(transport, PinnedOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the environment of a stdio server. In <see cref="EngineEnvironmentMode.Inherit"/> mode these are overrides on top of the inherited
    /// environment (a <c>null</c> value unsets a variable). In <see cref="EngineEnvironmentMode.Allowlist"/> mode the server inherits nothing
    /// (<see cref="StdioClientTransportOptions.InheritEnvironmentVariables"/> is <see langword="false"/>) and the result is the allowed subset of
    /// <paramref name="inheritedEnvironment"/> plus the explicit variables, which win. Names and values are never logged.
    /// </summary>
    /// <param name="options">How to launch the server.</param>
    /// <param name="inheritedEnvironment">Reads the variables of this process, which the server would inherit. Called only in <see cref="EngineEnvironmentMode.Allowlist"/> mode.</param>
    /// <returns>The environment, or <see langword="null"/> when there is nothing to set.</returns>
    internal static Dictionary<string, string?>? BuildEnvironment(McpStdioServerOptions options, Func<IEnumerable<KeyValuePair<string, string>>> inheritedEnvironment)
    {
        var policy = new EngineEnvironmentPolicy(options.EnvironmentMode, options.EnvironmentAllowList);
        if (policy.Mode == EngineEnvironmentMode.Inherit)
        {
            return options.EnvironmentVariables is null
                ? null
                : new Dictionary<string, string?>(options.EnvironmentVariables, StringComparer.Ordinal);
        }

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in inheritedEnvironment())
        {
            if (policy.IsAllowed(name))
            {
                environment[name] = value;
            }
        }

        if (options.EnvironmentVariables is not null)
        {
            foreach (var (name, value) in options.EnvironmentVariables)
            {
                if (value is null)
                {
                    _ = environment.Remove(name);
                }
                else
                {
                    environment[name] = value;
                }
            }
        }

        return environment;
    }

    private static List<KeyValuePair<string, string>> CurrentEnvironment()
    {
        var variables = new List<KeyValuePair<string, string>>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value)
            {
                variables.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        return variables;
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
