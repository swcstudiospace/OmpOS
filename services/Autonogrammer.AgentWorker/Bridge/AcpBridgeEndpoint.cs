using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Autonogrammer.AgentWorker.Configuration;
using Autonogrammer.AgentWorker.Hooks;
using Autonogrammer.AgentWorker.Security;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// The <c>GET /acp</c> endpoint: authenticates the upgrade request with the worker bearer token, claims the session named by the token
/// subject, starts <c>omp acp</c> in the session directory, and bridges its stdio protocol to the WebSocket. Failures before the upgrade
/// are plain HTTP statuses: 401 missing or invalid token, 403 the subject is not a session this worker serves, 400 not a WebSocket request,
/// 409 the session already has an attached connection, 503 omp could not be started.
/// </summary>
internal static class AcpBridgeEndpoint
{
    /// <summary>Gets the route of the bridge.</summary>
    public const string Route = "/acp";

    private static readonly Action<ILogger, string, Exception?> Attached =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(30, "AcpBridgeAttached"), "Session {SessionId} attached.");

    private static readonly Action<ILogger, string, Exception?> Refused =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(31, "AcpBridgeRefused"), "Session {SessionId} refused: another connection is attached.");

    private static readonly Action<ILogger, string, Exception?> StartFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(32, "AcpBridgeStartFailed"), "Session {SessionId}: omp acp could not be started.");

    /// <summary>Maps <c>GET /acp</c>, which requires an authenticated worker token.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapAcpBridge(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGet(Route, HandleAsync).RequireAuthorization();
    }

    private static async Task HandleAsync(
        HttpContext context,
        WorkerJwtValidator validator,
        OmpProcessHost host,
        IOptions<WorkerOptions> options,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Autonogrammer.AgentWorker.AcpBridge");
        if (!validator.TryGetSessionId(context.User, out var sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var hooks = options.Value.Hooks;
        var consent = hooks.Enabled ? HooksConsentSnapshotReader.Read(context.User, hooks.MaxSnapshotHashes) : HooksConsentRead.Absent;
        var attachment = host.TryAttach(sessionId, BearerToken(context.Request), consent);
        if (attachment is null)
        {
            Refused(logger, sessionId, null);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        try
        {
            try
            {
                await attachment.StartAsync(context.RequestAborted).ConfigureAwait(false);
            }
            catch (ProcessStartHarnessException ex)
            {
                StartFailed(logger, sessionId, ex);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            Attached(logger, sessionId, null);
            await AcpBridgeSession.RunAsync(
                socket,
                attachment.Transport,
                sessionId,
                options.Value.CloseTimeout,
                logger,
                lifetime.ApplicationStopping,
                context.RequestAborted,
                attachment.HookFrames).ConfigureAwait(false);
        }
        finally
        {
            await attachment.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string BearerToken(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var header) && header.Parameter is { } token
            ? token
            : string.Empty;
}
