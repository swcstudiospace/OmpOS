using System.Net.WebSockets;
using System.Text.Json;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// The relay hub behind the connect route. It refuses a plain request and any request that carries an Origin, upgrades to a WebSocket,
/// challenges the device with a single-use nonce, verifies the device's hello against the key it registered, answers with the welcome, delivers
/// its queued commands, and keeps the newest socket for each device (D-14, D-16). A signal wakes the device's delivery loop, or, after a
/// revocation, sends the revoke frame and closes with 4001 (D-30). While the host stops it refuses new connections and closes every open socket
/// with 1001 (D-45). Acknowledgements, the sweeper and the audit writer are separate units.
/// </summary>
internal sealed class DispatchHub : IDispatchConnectHandler
{
    private const int HeartbeatSeconds = 15;
    private const int MaxStrikes = 3;
    private static readonly TimeSpan HelloDeadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PresenceLifetime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PresenceRelease = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CloseBound = TimeSpan.FromSeconds(5);
    private static readonly Action<ILogger, Exception?> DrainedMessage = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(7405, "DispatchHubDrained"),
        "The Dispatch hub closed its sockets for shutdown.");

    private readonly IDispatchStore _store;
    private readonly IDispatchPresence _presence;
    private readonly IDispatchAuthNonces _nonces;
    private readonly DeviceCredentialIssuer _issuer;
    private readonly IOptions<DispatchOptions> _options;
    private readonly TimeProvider _time;
    private readonly DispatchSessionRegistry _sessions;
    private readonly DispatchDeliveryLoop _delivery;
    private readonly DispatchMetrics _metrics;
    private readonly ILogger<DispatchHub> _logger;
    private volatile bool _draining;

    /// <summary>Initializes a new instance of the <see cref="DispatchHub"/> class and subscribes to the host's stop.</summary>
    /// <param name="store">The device store.</param>
    /// <param name="presence">The presence seam.</param>
    /// <param name="nonces">The single-use challenge nonces.</param>
    /// <param name="issuer">The credential issuer, which holds the hub verification key.</param>
    /// <param name="options">The Dispatch options.</param>
    /// <param name="time">The clock.</param>
    /// <param name="sessions">The sockets this process holds.</param>
    /// <param name="delivery">The delivery loop each socket runs after the welcome.</param>
    /// <param name="metrics">The Dispatch metrics.</param>
    /// <param name="lifetime">The host lifetime, whose stop starts the drain.</param>
    /// <param name="logger">The logger. It never receives a body, a token or a key.</param>
    public DispatchHub(
        IDispatchStore store,
        IDispatchPresence presence,
        IDispatchAuthNonces nonces,
        DeviceCredentialIssuer issuer,
        IOptions<DispatchOptions> options,
        TimeProvider time,
        DispatchSessionRegistry sessions,
        DispatchDeliveryLoop delivery,
        DispatchMetrics metrics,
        IHostApplicationLifetime lifetime,
        ILogger<DispatchHub> logger)
    {
        _store = store;
        _presence = presence;
        _nonces = nonces;
        _issuer = issuer;
        _options = options;
        _time = time;
        _sessions = sessions;
        _delivery = delivery;
        _metrics = metrics;
        _logger = logger;
        ArgumentNullException.ThrowIfNull(lifetime);
        lifetime.ApplicationStopping.Register(BeginDrain);
    }

    /// <inheritdoc />
    public async Task<IResult> HandleAsync(HttpContext context, string deviceId, string accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.WebSockets.IsWebSocketRequest)
        {
            _metrics.Connect("refused", "not_websocket");
            return WebProblems.Of(StatusCodes.Status426UpgradeRequired, "upgrade_required", "WebSocket required.", "The connect route upgrades the request to a WebSocket.");
        }

        if (context.Request.Headers.ContainsKey("Origin"))
        {
            _metrics.Connect("refused", "origin_present");
            return WebProblems.Of(StatusCodes.Status403Forbidden, "origin_refused", "Origin refused.", "A device connection carries no Origin header.");
        }

        if (_draining)
        {
            _metrics.Connect("refused", "draining");
            context.Response.Headers["Retry-After"] = "5";
            return WebProblems.Of(StatusCodes.Status503ServiceUnavailable, "hub_draining", "The hub is stopping.", "Reconnect shortly.");
        }

        var device = await _store.GetDeviceAsync(accountId, deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null || device.RevokedAt is not null)
        {
            _metrics.Connect("refused", "device_not_found");
            return WebProblems.Of(StatusCodes.Status404NotFound, "device_not_found", "Device not found.", "No device with that identifier is paired to this account.");
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        await RunAsync(socket, context, device, cancellationToken).ConfigureAwait(false);
        return Results.Empty;
    }

    /// <summary>Handles one signal for a device: wakes each of its sockets' delivery loops, or revokes the sockets when the device was revoked (D-30).</summary>
    /// <param name="deviceId">The device the signal names. A signal is a hint, never a command.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when every socket for the device has been handled.</returns>
    public async Task OnSignalAsync(string deviceId, CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.ForDevice(deviceId))
        {
            var device = await _store.GetDeviceAsync(session.Owner, session.DeviceId, cancellationToken).ConfigureAwait(false);
            if (device is null || device.RevokedAt is not null)
            {
                await session.SendAsync(new DispatchRevokeFrame { V = 1, T = DispatchFrameTypes.Revoke, Reason = "revoked" }, DispatchJsonContext.Default.DispatchRevokeFrame, cancellationToken).ConfigureAwait(false);
                await session.CloseAsync(DispatchCloseCodes.DeviceRevoked, "revoked", cancellationToken).ConfigureAwait(false);
                continue;
            }

            session.Wake();
        }
    }

    private async Task RunAsync(WebSocket socket, HttpContext context, DispatchDeviceRecord device, CancellationToken cancellationToken)
    {
        var session = new DispatchHubSession(device.AccountId, device.DeviceId, Guid.NewGuid().ToString("N"), socket);
        try
        {
            var nonce = DispatchHandshake.NewNonce();
            var origin = HubOrigin(context);
            if (!await _nonces.TryClaimAsync(DispatchHandshake.HashNonce(nonce), DispatchHandshake.NonceLifetime, cancellationToken).ConfigureAwait(false))
            {
                await session.CloseAsync(DispatchCloseCodes.InternalError, "nonce_unavailable", cancellationToken).ConfigureAwait(false);
                return;
            }

            var challenge = new DispatchChallengeFrame
            {
                V = 1,
                T = DispatchFrameTypes.Challenge,
                Nonce = nonce,
                ServerTime = _time.GetUtcNow().ToUnixTimeSeconds(),
                Origin = origin,
                Protocols = ["1"],
            };
            await session.SendAsync(challenge, DispatchJsonContext.Default.DispatchChallengeFrame, cancellationToken).ConfigureAwait(false);

            var hello = await ReadHelloAsync(session, cancellationToken).ConfigureAwait(false);
            if (hello is null || !await VerifyHelloAsync(session, device, nonce, origin, hello, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await AdmitAsync(session, hello, cancellationToken).ConfigureAwait(false);
            await DeliverAndPumpAsync(session, hello.LastAppliedSeq, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // The peer dropped the connection. The session ends below.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping. The drain has closed the socket.
        }
        finally
        {
            if (_sessions.Remove(session))
            {
                await _presence.RefreshAsync(session.Owner, session.DeviceId, session.Holder, PresenceRelease, CancellationToken.None).ConfigureAwait(false);
            }

            session.Dispose();
        }
    }

    private async Task DeliverAndPumpAsync(DispatchHubSession session, long resumeFromSeq, CancellationToken cancellationToken)
    {
        using var delivering = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var deliveries = _delivery.RunAsync(session, resumeFromSeq, delivering.Token);
        try
        {
            await PumpAsync(session, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await delivering.CancelAsync().ConfigureAwait(false);
            await deliveries.ConfigureAwait(false);
        }
    }

    private async Task<DispatchHelloFrame?> ReadHelloAsync(DispatchHubSession session, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(HelloDeadline);
        DispatchHubSession.Inbound message;
        try
        {
            message = await session.ReceiveAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.Connect("refused", "hello_timeout");
            await session.CloseAsync(DispatchCloseCodes.CredentialInvalid, "hello_timeout", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (message.Kind == DispatchHubSession.InboundKind.TooLarge)
        {
            await session.CloseAsync(DispatchCloseCodes.MessageTooBig, "frame_too_big", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (message.Kind != DispatchHubSession.InboundKind.Text || !DispatchFrameGuard.TryAdmit(message.Payload, out var admission))
        {
            await session.CloseAsync(DispatchCloseCodes.InvalidPayload, "invalid_frame", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (!string.Equals(admission.Type, DispatchFrameTypes.Hello, StringComparison.Ordinal))
        {
            await session.CloseAsync(DispatchCloseCodes.PolicyViolation, "hello_required", cancellationToken).ConfigureAwait(false);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(message.Payload, DispatchJsonContext.Default.DispatchHelloFrame);
        }
        catch (JsonException)
        {
            await session.CloseAsync(DispatchCloseCodes.InvalidPayload, "invalid_frame", cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<bool> VerifyHelloAsync(DispatchHubSession session, DispatchDeviceRecord device, string nonce, string origin, DispatchHelloFrame hello, CancellationToken cancellationToken)
    {
        if (!string.Equals(hello.DeviceId, device.DeviceId, StringComparison.Ordinal)
            || !string.Equals(hello.Nonce, nonce, StringComparison.Ordinal)
            || !await _nonces.TryConsumeAsync(DispatchHandshake.HashNonce(nonce), cancellationToken).ConfigureAwait(false))
        {
            _metrics.Connect("refused", "nonce_invalid");
            await session.CloseAsync(DispatchCloseCodes.CredentialInvalid, "nonce_invalid", cancellationToken).ConfigureAwait(false);
            return false;
        }

        byte[] canonical;
        try
        {
            canonical = DispatchHelloCanonical.Build(hello.Nonce, hello.DeviceId, origin, hello.LastAppliedSeq);
        }
        catch (ArgumentException)
        {
            _metrics.Connect("refused", "hello_invalid");
            await session.CloseAsync(DispatchCloseCodes.CredentialInvalid, "hello_invalid", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (device.PublicKey is null || !DispatchHandshake.VerifyHello(device.PublicKey, canonical, hello.Signature))
        {
            _metrics.Connect("refused", "signature_invalid");
            await session.CloseAsync(DispatchCloseCodes.CredentialInvalid, "signature_invalid", cancellationToken).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task AdmitAsync(DispatchHubSession session, DispatchHelloFrame hello, CancellationToken cancellationToken)
    {
        var previous = _sessions.Replace(session);
        if (previous is not null)
        {
            using var bound = new CancellationTokenSource(CloseBound);
            await previous.CloseAsync(DispatchCloseCodes.Replaced, "replaced", bound.Token).ConfigureAwait(false);
        }

        await _presence.SetAsync(session.Owner, session.DeviceId, session.Holder, PresenceLifetime, cancellationToken).ConfigureAwait(false);
        if (hello.Capabilities is { } capabilities)
        {
            _ = await _store.UpdateCapabilitySnapshotAsync(session.Owner, session.DeviceId, capabilities, cancellationToken).ConfigureAwait(false);
        }

        var welcome = new DispatchWelcomeFrame
        {
            V = 1,
            T = DispatchFrameTypes.Welcome,
            ServerTime = _time.GetUtcNow().ToUnixTimeSeconds(),
            HeartbeatSeconds = HeartbeatSeconds,
            Kids = [_issuer.HubKey().Kid],
            ResumeFromSeq = hello.LastAppliedSeq,
        };
        await session.SendAsync(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame, cancellationToken).ConfigureAwait(false);
        _metrics.Connect("accepted", "welcome");
    }

    private async Task PumpAsync(DispatchHubSession session, CancellationToken cancellationToken)
    {
        var strikes = 0;
        while (session.IsOpen)
        {
            var message = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            switch (message.Kind)
            {
                case DispatchHubSession.InboundKind.Close:
                    return;
                case DispatchHubSession.InboundKind.Binary:
                    continue;
                case DispatchHubSession.InboundKind.TooLarge:
                    await session.CloseAsync(DispatchCloseCodes.MessageTooBig, "frame_too_big", cancellationToken).ConfigureAwait(false);
                    return;
            }

            if (!DispatchFrameGuard.TryAdmit(message.Payload, out var admission))
            {
                if (admission.ErrCode is null)
                {
                    await session.CloseAsync(DispatchCloseCodes.InvalidPayload, "invalid_frame", cancellationToken).ConfigureAwait(false);
                    return;
                }

                strikes++;
                await session.SendAsync(DispatchFrameGuard.UnsupportedFrame(), DispatchJsonContext.Default.DispatchErrFrame, cancellationToken).ConfigureAwait(false);
                if (strikes >= MaxStrikes)
                {
                    await session.CloseAsync(DispatchCloseCodes.PolicyViolation, "unsupported_frame", cancellationToken).ConfigureAwait(false);
                    return;
                }

                continue;
            }

            if (string.Equals(admission.Type, DispatchFrameTypes.Hb, StringComparison.Ordinal))
            {
                if (!await _presence.RefreshAsync(session.Owner, session.DeviceId, session.Holder, PresenceLifetime, cancellationToken).ConfigureAwait(false))
                {
                    await session.CloseAsync(DispatchCloseCodes.Replaced, "replaced", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
            else if (string.Equals(admission.Type, DispatchFrameTypes.Hello, StringComparison.Ordinal))
            {
                await session.CloseAsync(DispatchCloseCodes.PolicyViolation, "hello_repeated", cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }

    private void BeginDrain()
    {
        _draining = true;
        _ = Task.Run(CloseAllAsync);
    }

    private async Task CloseAllAsync()
    {
        var closes = _sessions.Snapshot().Select(async session =>
        {
            using var bound = new CancellationTokenSource(CloseBound);
            await session.CloseAsync(DispatchCloseCodes.GoingAway, "draining", bound.Token).ConfigureAwait(false);
        });
        await Task.WhenAll(closes).ConfigureAwait(false);
        DrainedMessage(_logger, null);
    }

    private string HubOrigin(HttpContext context) =>
        string.IsNullOrWhiteSpace(_options.Value.HubOrigin) ? $"{context.Request.Scheme}://{context.Request.Host}" : _options.Value.HubOrigin;
}
