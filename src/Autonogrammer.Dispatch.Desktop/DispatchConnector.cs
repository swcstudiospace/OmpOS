using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The outbound-only desktop link. It connects with the stored credential over the unchanged WebSocket transport, verifies every command
/// before anything runs, applies each command exactly once, and follows the close-code policy. Nothing is executed for a refused frame.
/// </summary>
public sealed class DispatchConnector
{
    /// <summary>The longest result text sent in one result frame.</summary>
    public const int MaxResultTextChars = 4096;

    private readonly DispatchConnectorOptions _options;
    private readonly DispatchPairing _pairing;
    private readonly DispatchDedupeStore _dedupe;
    private readonly DispatchApprovalFlow _approvals;
    private readonly IDispatchRevocationSink _revocation;
    private readonly DispatchCommandRouter _router;
    private readonly TimeProvider _clock;
    private readonly DispatchReconnectLoop _reconnect;
    private readonly Lock _gate = new();
    private DispatchConnectionState _state = DispatchConnectionState.Connecting;
    private DispatchVerifier? _verifier;
    private TimeSpan _clockOffset;
    private bool _welcomed;
    private bool _helloSent;
    private int _heartbeatSeconds;
    private int _runningCount;
    private string _deviceStatus = "idle";

    /// <summary>Creates the connector.</summary>
    /// <param name="options">The endpoint and the dialog and configuration values.</param>
    /// <param name="pairing">The pairing state and the secret store.</param>
    /// <param name="dedupe">The persisted exactly-once record.</param>
    /// <param name="policy">The desktop policy.</param>
    /// <param name="approvals">The local approval flow.</param>
    /// <param name="revocation">Ends the sessions this desktop started for Dispatch on revocation.</param>
    /// <param name="handlers">The command handlers.</param>
    /// <param name="clock">The local clock.</param>
    /// <param name="jitter">The source of the reconnect jitter.</param>
    public DispatchConnector(
        DispatchConnectorOptions options,
        DispatchPairing pairing,
        DispatchDedupeStore dedupe,
        DispatchPolicyStore policy,
        DispatchApprovalFlow approvals,
        IDispatchRevocationSink revocation,
        IEnumerable<IDispatchCommandHandler> handlers,
        TimeProvider clock,
        IJitterSource jitter)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _dedupe = dedupe ?? throw new ArgumentNullException(nameof(dedupe));
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _revocation = revocation ?? throw new ArgumentNullException(nameof(revocation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentNullException.ThrowIfNull(policy);
        _router = new DispatchCommandRouter([.. handlers], policy, approvals, options.ComputerUse, options.ControllerName);
        _reconnect = new DispatchReconnectLoop(clock, jitter ?? throw new ArgumentNullException(nameof(jitter)));
    }

    /// <summary>Raised after the state changes. It may run on the connection's thread.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Gets what the desktop shows about its link.</summary>
    public DispatchConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Connects and serves commands until the link reaches a terminal state. A transient failure retries forever on the reconnect schedule.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that completes when the connector stops for a terminal state.</returns>
    /// <exception cref="InvalidOperationException">The desktop is not paired.</exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var paired = _pairing.LoadPairing() ?? throw new InvalidOperationException("The desktop is not paired with Dispatch.");
        using var ring = new DispatchKeyRing(paired.Keys);
        _verifier = new DispatchVerifier(ring, paired.AccountId, paired.DeviceId, _clock);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var decision = await AttemptAsync(paired.DeviceId, cancellationToken).ConfigureAwait(false);
                SetState(decision.State);
                if (decision.State == DispatchConnectionState.DeviceRevoked)
                {
                    // Revocation ends what this desktop started for Dispatch and withdraws its grants.
                    _approvals.Withdraw();
                    await _revocation.EndDispatchedSessionsAsync(cancellationToken).ConfigureAwait(false);
                }

                if (decision.Action == DispatchLinkAction.Stop)
                {
                    return;
                }

                var delay = decision.Action == DispatchLinkAction.RetryWithJitter ? _reconnect.NextJitteredDelay() : decision.Delay;
                await Task.Delay(delay, _clock, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _verifier = null;
            _welcomed = false;
        }
    }

    private static DispatchLinkDecision Stop(DispatchConnectionState state) => new(DispatchLinkAction.Stop, state, TimeSpan.Zero);

    private async Task<DispatchLinkDecision> AttemptAsync(string deviceId, CancellationToken cancellationToken)
    {
        SetState(DispatchConnectionState.Connecting);
        SecretString? credential;
        try
        {
            // Fail closed: the device key must be readable before anything connects. There is no file fallback.
            if (!await _pairing.HasDeviceKeyAsync(deviceId, cancellationToken).ConfigureAwait(false))
            {
                return Stop(DispatchConnectionState.KeyStoreUnavailable);
            }

            credential = await _pairing.ReadCredentialAsync(deviceId, cancellationToken).ConfigureAwait(false);
        }
        catch (DispatchKeyStoreUnavailableException)
        {
            return Stop(DispatchConnectionState.KeyStoreUnavailable);
        }

        if (credential is null)
        {
            return Stop(DispatchConnectionState.CredentialExpired);
        }

        var token = credential.Reveal();
        var expiry = DispatchPairing.ReadCredentialExpiry(token);
        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions
        {
            Endpoint = _options.Endpoint,
            BearerTokenProvider = _ => ValueTask.FromResult(token),
        });
        await using (transport.ConfigureAwait(false))
        {
            return await ServeLinkAsync(transport, expiry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<DispatchLinkDecision> ServeLinkAsync(AcpWebSocketTransport transport, DateTimeOffset? expiry, CancellationToken cancellationToken)
    {
        _helloSent = false;
        _welcomed = false;
        try
        {
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessStartHarnessException ex)
        {
            return DispatchCloseStatus.ClassifyUpgrade(ex.Message, expiry, _clock.GetUtcNow());
        }

        _reconnect.MarkConnected();
        SetState(DispatchConnectionState.Connected);
        using var link = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? heartbeat = null;
        try
        {
            await foreach (var frame in transport.ReceiveAsync(link.Token).WithCancellation(link.Token).ConfigureAwait(false))
            {
                var stop = await HandleFrameAsync(transport, frame, link.Token).ConfigureAwait(false);
                if (_welcomed && heartbeat is null)
                {
                    heartbeat = SendHeartbeatsAsync(transport, link.Cancel, link.Token);
                }
                if (stop is { } decision)
                {
                    return decision;
                }
            }

            // The stream ended without a close status: a network error.
            return DispatchCloseStatus.ClassifyClose(null);
        }
        catch (DisconnectedHarnessException ex)
        {
            return DispatchCloseStatus.ClassifyClose(ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && link.IsCancellationRequested)
        {
            return DispatchCloseStatus.ClassifyClose(null);
        }
        finally
        {
            await link.CancelAsync().ConfigureAwait(false);
            if (heartbeat is not null)
            {
                try
                {
                    await heartbeat.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (link.IsCancellationRequested)
                {
                    // The connection is ending.
                }
            }

            _reconnect.MarkDisconnected();
        }
    }

    private async Task<DispatchLinkDecision?> HandleFrameAsync(AcpWebSocketTransport transport, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (!DispatchFrameGuard.TryAdmit(frame.Span, out var admission) || !admission.Accepted)
        {
            return null;
        }

        switch (admission.Type)
        {
            case DispatchFrameTypes.Challenge:
                return await RespondToChallengeAsync(transport, frame, cancellationToken).ConfigureAwait(false);
            case DispatchFrameTypes.Welcome:
                if (_helloSent)
                {
                    AdoptWelcome(frame);
                }
                return null;
            case DispatchFrameTypes.Cmd:
                await HandleCommandAsync(transport, frame, cancellationToken).ConfigureAwait(false);
                return null;
            case DispatchFrameTypes.Revoke:
                return Stop(DispatchConnectionState.DeviceRevoked);
            default:
                return null;
        }
    }

    private async Task<DispatchLinkDecision?> RespondToChallengeAsync(AcpWebSocketTransport transport, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (_helloSent || _welcomed)
        {
            return Stop(DispatchConnectionState.AccountUnavailable);
        }

        DispatchChallengeFrame? challenge;
        try
        {
            challenge = JsonSerializer.Deserialize(frame.Span, DispatchJsonContext.Default.DispatchChallengeFrame);
        }
        catch (JsonException)
        {
            return Stop(DispatchConnectionState.AccountUnavailable);
        }

        var paired = _pairing.LoadPairing();
        var endpoint = _options.Endpoint;
        var origin = new UriBuilder(endpoint) { Scheme = string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http" }.Uri.GetLeftPart(UriPartial.Authority);
        if (paired is null || challenge is null || !string.Equals(challenge.Origin, origin, StringComparison.OrdinalIgnoreCase)
            || challenge.Protocols is null || !challenge.Protocols.Contains("1", StringComparer.Ordinal))
        {
            return Stop(DispatchConnectionState.AccountUnavailable);
        }

        try
        {
            var sequence = _dedupe.HighWater;
            var canonical = DispatchHelloCanonical.Build(challenge.Nonce, paired.DeviceId, origin, sequence);
            var signature = await _pairing.SignHelloAsync(paired.DeviceId, canonical, cancellationToken).ConfigureAwait(false);
            var hello = new DispatchHelloFrame
            {
                V = 1,
                T = DispatchFrameTypes.Hello,
                DeviceId = paired.DeviceId,
                Nonce = challenge.Nonce,
                Origin = origin,
                LastAppliedSeq = sequence,
                Signature = Convert.ToBase64String(signature).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            };
            await transport.SendAsync(JsonSerializer.SerializeToUtf8Bytes(hello, DispatchJsonContext.Default.DispatchHelloFrame), cancellationToken).ConfigureAwait(false);
            _helloSent = true;
            return null;
        }
        catch (DispatchKeyStoreUnavailableException)
        {
            return Stop(DispatchConnectionState.KeyStoreUnavailable);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or System.Security.Cryptography.CryptographicException)
        {
            return Stop(DispatchConnectionState.AccountUnavailable);
        }
    }

    private void AdoptWelcome(ReadOnlyMemory<byte> frame)
    {
        DispatchWelcomeFrame? welcome;
        try
        {
            welcome = JsonSerializer.Deserialize(frame.Span, DispatchJsonContext.Default.DispatchWelcomeFrame);
        }
        catch (JsonException)
        {
            return;
        }

        if (welcome is null || welcome.HeartbeatSeconds <= 0 || _welcomed)
        {
            return;
        }

        _clockOffset = TimeSpan.FromSeconds(welcome.ServerTime - _clock.GetUtcNow().ToUnixTimeSeconds());
        _heartbeatSeconds = welcome.HeartbeatSeconds;
        _welcomed = true;
    }

    private async Task HandleCommandAsync(AcpWebSocketTransport transport, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        // Commands before the hub's welcome carry no clock offset. The device refuses them rather than guess.
        if (!_welcomed || _verifier is null)
        {
            return;
        }

        DispatchCmdFrame? cmd;
        try
        {
            cmd = JsonSerializer.Deserialize(frame.Span, DispatchJsonContext.Default.DispatchCmdFrame);
        }
        catch (JsonException)
        {
            return;
        }

        if (cmd is null || !_verifier.Verify(cmd, _clockOffset).Accepted)
        {
            return;
        }

        if (!DispatchOpWire.TryParse(cmd.Op, out var op) || !DispatchCapabilityWire.TryParse(cmd.Capability, out var capability)
            || !DispatchBase64Url.TryDecode(cmd.Payload, out var payload))
        {
            return;
        }

        switch (_dedupe.Admit(cmd.CommandId, cmd.Nonce, cmd.Seq))
        {
            case DispatchAdmission.Duplicate:
                await SendAckAsync(transport, cmd.CommandId, "duplicate", cancellationToken).ConfigureAwait(false);
                return;
            case DispatchAdmission.Replayed:
                return;
            default:
                break;
        }

        // The command is persisted as applied; only now is it acknowledged and executed.
        await SendAckAsync(transport, cmd.CommandId, DispatchCommandStateWire.ToWire(DispatchCommandState.Received), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _runningCount, 1);
        try
        {
            var outcome = await _router.RouteAsync(
                new DispatchCommand(cmd.CommandId, capability, op, payload),
                (state, token) => ReportStateAsync(transport, cmd.CommandId, state, token),
                cancellationToken).ConfigureAwait(false);
            await SendResultAsync(transport, cmd.CommandId, outcome, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _runningCount, 0);
            Volatile.Write(ref _deviceStatus, "idle");
        }
    }

    private async ValueTask ReportStateAsync(AcpWebSocketTransport transport, string commandId, DispatchCommandState state, CancellationToken cancellationToken)
    {
        await SendAckAsync(transport, commandId, DispatchCommandStateWire.ToWire(state), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _deviceStatus, state == DispatchCommandState.AwaitingApproval ? "approval_pending" : "busy");
    }

    private async Task SendHeartbeatsAsync(AcpWebSocketTransport transport, Action cancelLink, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_heartbeatSeconds), _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var heartbeat = new DispatchHbFrame
                {
                    V = 1,
                    T = DispatchFrameTypes.Hb,
                    LastAppliedSeq = _dedupe.HighWater,
                    RunningCount = Volatile.Read(ref _runningCount),
                    Status = Volatile.Read(ref _deviceStatus),
                };
                await transport.SendAsync(JsonSerializer.SerializeToUtf8Bytes(heartbeat, DispatchJsonContext.Default.DispatchHbFrame), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DisconnectedHarnessException)
        {
            cancelLink();
        }
    }

    private static async Task SendAckAsync(AcpWebSocketTransport transport, string commandId, string state, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new DispatchAckFrame { V = 1, T = DispatchFrameTypes.Ack, CommandId = commandId, State = state },
            DispatchJsonContext.Default.DispatchAckFrame);
        await transport.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendResultAsync(AcpWebSocketTransport transport, string commandId, DispatchOutcome outcome, CancellationToken cancellationToken)
    {
        var text = outcome.Text;
        if (text is { Length: > MaxResultTextChars })
        {
            text = text[..MaxResultTextChars];
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new DispatchResFrame
            {
                V = 1,
                T = DispatchFrameTypes.Res,
                CommandId = commandId,
                State = DispatchCommandStateWire.ToWire(outcome.State),
                Reason = outcome.Reason is { } reason ? DispatchFailureReasonWire.ToWire(reason) : null,
                Text = text,
            },
            DispatchJsonContext.Default.DispatchResFrame);
        await transport.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private void SetState(DispatchConnectionState state)
    {
        bool changed;
        lock (_gate)
        {
            changed = _state != state;
            _state = state;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
