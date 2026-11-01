using System.Net;
using System.Text.Json;
using Autonogrammer.Harness.Mcp.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>
/// Checks one server when the caller asks: connect, handshake, list tools, disconnect. Nothing starts a check at construction
/// or on a timer. Expected failures are <see cref="McpConnectionOutcome.Failed"/> results with fixed text, never exceptions,
/// except cancellation of the caller's token, which aborts the check.
/// </summary>
public sealed class McpConnectionChecker : IMcpConnectionChecker
{
    /// <summary>The reason used when the server asks for sign-in.</summary>
    public const string ConnectorSignInNeeded = "ConnectorSignInNeeded";

    /// <summary>The reason used when a check fails for any expected reason other than a timeout.</summary>
    public const string ConnectorCheckFailed = "ConnectorCheckFailed";

    /// <summary>The reason used when a check exceeds <see cref="McpConnectionCheckOptions.Timeout"/>.</summary>
    public const string ConnectorCheckTimedOut = "ConnectorCheckTimedOut";

    private const string PluginOriginPrefix = "plugin:";

    private static readonly Action<ILogger, string, Exception?> CheckFailed =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "McpConnectionCheckFailed"),
            "MCP connection check failed with {ExceptionType}.");

    private readonly McpConnectionCheckOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    /// <summary>Creates a checker that runs only when <see cref="CheckAsync"/> is called.</summary>
    /// <param name="options">Timeout and stdio environment policy. Null uses the defaults.</param>
    /// <param name="time">Measures the timeout. Null uses <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Receives the exception type of a failed check, never a message. Null discards it.</param>
    public McpConnectionChecker(McpConnectionCheckOptions? options = null, TimeProvider? time = null, ILogger? logger = null)
    {
        _options = options ?? new McpConnectionCheckOptions();
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var work = RunAsync(server, operation.Token);
        var finished = await Task.WhenAny(work, Task.Delay(_options.Timeout, _time, CancellationToken.None)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, work))
        {
            await operation.CancelAsync().ConfigureAwait(false);
            try
            {
                _ = await work.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Failed(ConnectorCheckTimedOut);
        }

        return await work.ConfigureAwait(false);
    }

    private async Task<McpConnectionResult> RunAsync(McpServerSpec server, CancellationToken cancellationToken)
    {
        McpClient? client = null;
        try
        {
            client = await ConnectAsync(server, cancellationToken).ConfigureAwait(false);
            var listed = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var tools = new List<McpToolDescriptor>();
            foreach (var tool in listed)
            {
                var schema = tool.ProtocolTool.InputSchema.ValueKind == JsonValueKind.Undefined
                    ? EmptySchema()
                    : tool.ProtocolTool.InputSchema.Clone();
                tools.Add(new McpToolDescriptor(new McpToolId(server.Name, tool.Name), tool.ProtocolTool.Description, schema));
            }

            return new McpConnectionResult(McpConnectionOutcome.Connected, null, tools.Count, tools);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(ConnectorCheckTimedOut);
        }
        catch (TimeoutException exception)
        {
            CheckFailed(_logger, exception.GetType().Name, null);
            return Failed(ConnectorCheckTimedOut);
        }
        catch (Exception exception) when (IsSignIn(exception))
        {
            CheckFailed(_logger, exception.GetType().Name, null);
            return new McpConnectionResult(McpConnectionOutcome.AuthRequired, ConnectorSignInNeeded, 0, null);
        }
#pragma warning disable CA1031 // A check is a boundary: every expected failure is a fixed result, never an exception, and the log names the type only.
        catch (Exception exception)
        {
            CheckFailed(_logger, exception.GetType().Name, null);
            return Failed(ConnectorCheckFailed);
        }
#pragma warning restore CA1031
        finally
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<McpClient> ConnectAsync(McpServerSpec server, CancellationToken cancellationToken)
    {
        if (server.Transport == McpTransportKind.Stdio)
        {
            if (string.IsNullOrWhiteSpace(server.Command))
            {
                throw new InvalidOperationException(ConnectorCheckFailed);
            }

            var mode = server.Origin.StartsWith(PluginOriginPrefix, StringComparison.Ordinal)
                ? _options.BundleEnvironmentMode
                : _options.EnvironmentMode;
            var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in server.Env)
            {
                environment[pair.Key] = pair.Value;
            }

            return await McpClientConnector.ConnectStdioAsync(
                new McpStdioServerOptions(
                    server.Command,
                    server.Args,
                    AppContext.BaseDirectory,
                    environment,
                    mode,
                    _options.EnvironmentAllowList),
                cancellationToken).ConfigureAwait(false);
        }

        if (server.Url is null)
        {
            throw new InvalidOperationException(ConnectorCheckFailed);
        }

        return await McpClientConnector.ConnectHttpAsync(server.Url, cancellationToken).ConfigureAwait(false);
    }

    private static McpConnectionResult Failed(string reason) =>
        new(McpConnectionOutcome.Failed, reason, 0, null);

    private static JsonElement EmptySchema()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static bool IsSignIn(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException http && http.StatusCode == HttpStatusCode.Unauthorized)
            {
                return true;
            }

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(IsSignIn))
            {
                return true;
            }
        }

        return false;
    }
}
