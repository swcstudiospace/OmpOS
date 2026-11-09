using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Dispatch.Desktop.Tests.Support;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Acp.Tests;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

public sealed class ReconnectTests : IDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_710_000_000);

    private readonly List<IDisposable> _disposables = [];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dispatch-reconnect-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void TheJitteredDelayDoublesAndCapsAtThirtySeconds()
    {
        var loop = new DispatchReconnectLoop(new ManualTimeProvider(Start), new FixedJitterSource(0.5));

        var delays = Enumerable.Range(0, 8).Select(_ => loop.NextJitteredDelay().TotalSeconds).ToArray();

        // Uniform in [0, min(30, 2^attempt)); the sample 0.5 sits at the middle of each window.
        delays.ShouldBe(new double[] { 0.5, 1, 2, 4, 8, 15, 15, 15 });
    }

    [Fact]
    public void EveryJitteredDelayStaysBelowItsWindow()
    {
        var loop = new DispatchReconnectLoop(new ManualTimeProvider(Start), new FixedJitterSource(0.9));

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var window = Math.Min(30, Math.Pow(2, attempt));
            loop.NextJitteredDelay().TotalSeconds.ShouldBeLessThan(window);
        }
    }

    [Fact]
    public void TheCounterResetsOnlyAfterAConnectionStayedUpForSixtySeconds()
    {
        var clock = new ManualTimeProvider(Start);
        var loop = new DispatchReconnectLoop(clock, new FixedJitterSource(0.5));
        loop.NextJitteredDelay();
        loop.NextJitteredDelay();
        loop.NextJitteredDelay();
        loop.Attempt.ShouldBe(3);

        loop.MarkConnected();
        clock.Advance(TimeSpan.FromSeconds(59));
        loop.MarkDisconnected();
        loop.Attempt.ShouldBe(3);

        loop.MarkConnected();
        clock.Advance(TimeSpan.FromSeconds(61));
        loop.MarkDisconnected();
        loop.Attempt.ShouldBe(0);
    }

    [Theory]
    [InlineData("The cloud worker closed the connection (4001 revoked).", DispatchLinkAction.Stop, DispatchConnectionState.DeviceRevoked)]
    [InlineData("The cloud worker closed the connection (4002 invalid).", DispatchLinkAction.Stop, DispatchConnectionState.CredentialExpired)]
    [InlineData("The cloud worker closed the connection (4010 update).", DispatchLinkAction.Stop, DispatchConnectionState.UpdateRequired)]
    [InlineData("The cloud worker closed the connection (4011 unavailable).", DispatchLinkAction.Stop, DispatchConnectionState.AccountUnavailable)]
    [InlineData("The cloud worker closed the connection (4009 replaced).", DispatchLinkAction.RetryAfter, DispatchConnectionState.ReplacedByOtherInstance)]
    [InlineData("The cloud worker closed the connection (1001 going away).", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    [InlineData("The cloud worker closed the connection (1013 try again).", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    [InlineData("The cloud worker closed the connection (no status).", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    [InlineData("something unrelated happened", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    public void EachCloseCodeMapsToItsSpecifiedBehaviour(string message, DispatchLinkAction action, DispatchConnectionState state)
    {
        var decision = DispatchCloseStatus.ClassifyClose(message);

        decision.Action.ShouldBe(action);
        decision.State.ShouldBe(state);
    }

    [Fact]
    public void ReplacedWaitsSixtySecondsAndRateLimitWaitsTheAdvertisedDelay()
    {
        DispatchCloseStatus.ClassifyClose("The cloud worker closed the connection (4009 replaced).").Delay.ShouldBe(TimeSpan.FromSeconds(60));
        DispatchCloseStatus.ClassifyClose("The cloud worker closed the connection (4020 retry after 12 seconds).").Delay
            .ShouldBe(TimeSpan.FromSeconds(12));
        DispatchCloseStatus.ClassifyClose("The cloud worker closed the connection (4020 slow down).").Action
            .ShouldBe(DispatchLinkAction.RetryWithJitter);
    }

    [Theory]
    [InlineData("Could not connect to the cloud worker (HTTP 503).", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    [InlineData("Could not connect to the cloud worker.", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.Reconnecting)]
    [InlineData("Could not connect to the cloud worker (HTTP 404).", DispatchLinkAction.RetryWithJitter, DispatchConnectionState.DispatchDisabled)]
    [InlineData("Could not connect to the cloud worker (HTTP 403).", DispatchLinkAction.Stop, DispatchConnectionState.AccountUnavailable)]
    [InlineData("Could not connect to the cloud worker (HTTP 426).", DispatchLinkAction.Stop, DispatchConnectionState.UpdateRequired)]
    public void AnUpgradeRefusalMapsItsStatus(string message, DispatchLinkAction action, DispatchConnectionState state)
    {
        var decision = DispatchCloseStatus.ClassifyUpgrade(message, null, Start);

        decision.Action.ShouldBe(action);
        decision.State.ShouldBe(state);
    }

    [Fact]
    public void A429WaitsAtLeastSixtySecondsAndA401SplitsOnTheStoredCredentialExpiry()
    {
        DispatchCloseStatus.ClassifyUpgrade("Could not connect to the cloud worker (HTTP 429).", null, Start).Delay
            .ShouldBe(TimeSpan.FromSeconds(60));

        var futureExpiry = Start.AddHours(1);
        var unauthorizedFuture = DispatchCloseStatus.ClassifyUpgrade("Could not connect to the cloud worker (HTTP 401).", futureExpiry, Start);
        unauthorizedFuture.State.ShouldBe(DispatchConnectionState.DeviceRevoked);

        var unauthorizedPast = DispatchCloseStatus.ClassifyUpgrade("Could not connect to the cloud worker (HTTP 401).", Start.AddHours(-1), Start);
        unauthorizedPast.State.ShouldBe(DispatchConnectionState.CredentialExpired);
    }

    [Fact]
    public async Task ARevokedCloseStopsTheConnectorAndEndsItsDispatchedSessions()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);

        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await session.CloseOutputAsync((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked, "revoked", cts.Token).ConfigureAwait(false);
            _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }, cts.Token);

        await fixture.Connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);

        fixture.Connector.State.ShouldBe(DispatchConnectionState.DeviceRevoked);
        fixture.Revocation.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(DispatchCloseCodes.CredentialInvalid, DispatchConnectionState.CredentialExpired)]
    [InlineData(DispatchCloseCodes.UnsupportedVersion, DispatchConnectionState.UpdateRequired)]
    [InlineData(DispatchCloseCodes.AccountUnavailable, DispatchConnectionState.AccountUnavailable)]
    public async Task ATerminalCloseStopsWithItsStateAndEndsNothing(int code, DispatchConnectionState expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);

        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await session.CloseOutputAsync((WebSocketCloseStatus)code, "stop", cts.Token).ConfigureAwait(false);
            _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }, cts.Token);

        await fixture.Connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);

        fixture.Connector.State.ShouldBe(expected);
        fixture.Revocation.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(401, DispatchConnectionState.DeviceRevoked, 1)]
    [InlineData(403, DispatchConnectionState.AccountUnavailable, 0)]
    [InlineData(426, DispatchConnectionState.UpdateRequired, 0)]
    public async Task APreUpgradeRefusalStopsWithItsState(int status, DispatchConnectionState expected, int revocationCalls)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new TestWebSocketServer();
        // The credential expires in the future, so a 401 means the device was revoked rather than that the credential aged out.
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);

        var serverSide = server.RejectAsync(status, cts.Token);

        await fixture.Connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);

        fixture.Connector.State.ShouldBe(expected);
        fixture.Revocation.Calls.ShouldBe(revocationCalls);
    }

    [Fact]
    public async Task AnUnavailableSecretStoreMeansTheDesktopDoesNotConnect()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var fixture = await Fixture.CreateAsync(_directory, new Uri("ws://127.0.0.1:1/acp"), Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var unavailable = new DispatchPairing(new UnavailableSecretStore(), fixture.PairingPath);
        var connector = fixture.BuildConnector(unavailable);

        await connector.RunAsync(cts.Token);

        connector.State.ShouldBe(DispatchConnectionState.KeyStoreUnavailable);
    }

    [Fact]
    public async Task AForgedCommandRunsNothingAndAValidCommandRunsOnce()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var counting = new CountingHandler();
        var connector = fixture.BuildConnector(fixture.Pairing, [counting]);

        using var attacker = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var forged = fixture.Signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-forged", nonce: "n-forged", signer: attacker);
        var valid = fixture.Signer.Sign(2, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-valid", nonce: "n-valid");
        var welcome = JsonSerializer.Serialize(
            new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 },
            DispatchJsonContext.Default.DispatchWelcomeFrame);

        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cts.Token).ConfigureAwait(false);
            await session.SendTextAsync(welcome, cts.Token).ConfigureAwait(false);
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(forged)), cts.Token).ConfigureAwait(false);
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(valid)), cts.Token).ConfigureAwait(false);

            // The forged command is ignored. The valid command reports each state before its terminal result.
            var received = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            var accepted = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            var running = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            var result = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            using var receivedJson = JsonDocument.Parse(received.Text);
            using var acceptedJson = JsonDocument.Parse(accepted.Text);
            using var runningJson = JsonDocument.Parse(running.Text);
            using var resultJson = JsonDocument.Parse(result.Text);
            receivedJson.RootElement.GetProperty("commandId").GetString().ShouldBe("cmd-valid");
            receivedJson.RootElement.GetProperty("state").GetString().ShouldBe("received");
            acceptedJson.RootElement.GetProperty("state").GetString().ShouldBe("accepted");
            runningJson.RootElement.GetProperty("state").GetString().ShouldBe("running");
            resultJson.RootElement.GetProperty("state").GetString().ShouldBe("completed");

            await session.CloseOutputAsync((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked, "revoked", cts.Token).ConfigureAwait(false);
            _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }, cts.Token);

        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);

        counting.Calls.ShouldBe(1);
        connector.State.ShouldBe(DispatchConnectionState.DeviceRevoked);
    }

    [Fact]
    public async Task AConnectedDesktopSendsHeartbeatsAtTheWelcomeInterval()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new TestWebSocketServer();
        var clock = new HeartbeatClock(Start);
        var fixture = await Fixture.CreateWithClockAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), clock, cts.Token);
        _disposables.Add(fixture);
        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cts.Token).ConfigureAwait(false);
            var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 1, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
            await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cts.Token).ConfigureAwait(false);
            await clock.TimerCreated.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            clock.Advance(TimeSpan.FromSeconds(1));
            var frame = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            using var heartbeat = JsonDocument.Parse(frame.Text);
            heartbeat.RootElement.GetProperty("t").GetString().ShouldBe("hb");
            heartbeat.RootElement.GetProperty("status").GetString().ShouldBe("idle");
            heartbeat.RootElement.GetProperty("runningCount").GetInt32().ShouldBe(0);
            fixture.Pause();
            clock.Advance(TimeSpan.FromSeconds(1));
            var pausedFrame = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            using var paused = JsonDocument.Parse(pausedFrame.Text);
            paused.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            await session.CloseOutputAsync((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked, "revoked", cts.Token).ConfigureAwait(false);
            _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await fixture.Connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
    }

    [Fact]
    public async Task RevocationCancelsAnExecutingCommandWithoutWaitingForItToFinish()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var handler = new BlockingHandler();
        var connector = fixture.BuildConnector(fixture.Pairing, [handler]);
        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cts.Token).ConfigureAwait(false);
            var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
            await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cts.Token).ConfigureAwait(false);
            var command = fixture.Signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-running", nonce: "n-running");
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(command)), cts.Token).ConfigureAwait(false);
            await handler.Started.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            for (var index = 0; index < 3; index++)
            {
                _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
            }

            var second = fixture.Signer.Sign(2, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-queued", nonce: "n-queued");
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(second)), cts.Token).ConfigureAwait(false);
            var acknowledgement = await session.ReceiveAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
            using var ack = JsonDocument.Parse(acknowledgement.Text);
            ack.RootElement.GetProperty("commandId").GetString().ShouldBe("cmd-queued");
            ack.RootElement.GetProperty("state").GetString().ShouldBe("received");
            await session.SendTextAsync("{\"v\":1,\"t\":\"revoke\",\"reason\":\"revoked\"}", cts.Token).ConfigureAwait(false);
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        connector.State.ShouldBe(DispatchConnectionState.DeviceRevoked);
        fixture.Revocation.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ShutdownAbortsAHeartbeatBlockedInAnActiveSocketWrite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var link = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var server = new TestWebSocketServer();
        const int frameSize = 32 * 1024 * 1024;
        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions
        {
            Endpoint = server.Endpoint,
            BearerTokenProvider = _ => ValueTask.FromResult("test"),
            MaxFrameBytes = frameSize,
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var accepting = server.AcceptAsync(timeout.Token);
        await transport.ConnectAsync(timeout.Token).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);
        var heartbeat = transport.SendAsync(new byte[frameSize], link.Token);
        await Task.Yield();
        heartbeat.IsCompleted.ShouldBeFalse();
        await link.CancelAsync().ConfigureAwait(true);
        await DispatchConnector.StopHeartbeatAsync(transport, heartbeat, link.Token).WaitAsync(TimeSpan.FromSeconds(5), timeout.Token).ConfigureAwait(true);
        heartbeat.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task AFullInboxStillReceivesRevocation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var handler = new BlockingHandler();
        var connector = fixture.BuildConnector(fixture.Pairing, [handler]);
        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cts.Token).ConfigureAwait(false);
            var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
            await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cts.Token).ConfigureAwait(false);
            for (var sequence = 1; sequence <= 32; sequence++)
            {
                var suffix = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var command = fixture.Signer.Sign(sequence, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-" + suffix, nonce: "nonce-" + suffix);
                await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(command)), cts.Token).ConfigureAwait(false);
                if (sequence == 1)
                {
                    await handler.Started.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                    _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
                    _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
                }

                var received = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
                using var ack = JsonDocument.Parse(received.Text);
                ack.RootElement.GetProperty("state").GetString().ShouldBe(sequence == 1 ? "running" : "received");
            }

            var overflow = fixture.Signer.Sign(33, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-33", nonce: "nonce-33");
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(overflow)), cts.Token).ConfigureAwait(false);
            await session.SendTextAsync("{\"v\":1,\"t\":\"revoke\",\"reason\":\"revoked\"}", cts.Token).ConfigureAwait(false);
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        connector.State.ShouldBe(DispatchConnectionState.DeviceRevoked);
        fixture.HighWater.ShouldBe(32);
    }
    [Fact]
    public async Task CancellationPassesAFullInboxWithoutSkippingDeferredWork()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var handler = new CancelFirstHandler();
        var connector = fixture.BuildConnector(fixture.Pairing, [handler]);
        var serverSide = Task.Run(async () =>
        {
            await CancelWithFullInboxAsync(server, fixture, handler, cts.Token).ConfigureAwait(false);
            await CompleteDeferredCommandAsync(server, fixture, handler, cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        handler.Calls.ShouldBe(32);
    }

    private static async Task CancelWithFullInboxAsync(TestWebSocketServer server, Fixture fixture, CancelFirstHandler handler, CancellationToken cancellationToken)
    {
        using var session = await server.AcceptAsync(cancellationToken).ConfigureAwait(false);
        await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cancellationToken).ConfigureAwait(false);
        var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
        await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cancellationToken).ConfigureAwait(false);
        for (var sequence = 1; sequence <= 32; sequence++)
        {
            var suffix = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var command = fixture.Signer.Sign(sequence, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-" + suffix, nonce: "nonce-" + suffix);
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(command)), cancellationToken).ConfigureAwait(false);
            if (sequence == 1)
            {
                await handler.Started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                _ = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }

            _ = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        var deferred = fixture.Signer.Sign(33, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-33", nonce: "nonce-33");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(deferred)), cancellationToken).ConfigureAwait(false);
        await RejectForgeryAndCancelQueuedAsync(session, fixture, handler, cancellationToken).ConfigureAwait(false);
        var cancel = fixture.Signer.Sign(37, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cancel-37", nonce: "nonce-37", payloadJson: "{\"commandId\":\"cmd-1\"}", op: "command.cancel");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(cancel)), cancellationToken).ConfigureAwait(false);
        await handler.Cancelled.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var results = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var message = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (message.Type == WebSocketMessageType.Close)
            {
                await session.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken).ConfigureAwait(false);
                break;
            }

            using var json = JsonDocument.Parse(message.Text);
            if (string.Equals(json.RootElement.GetProperty("t").GetString(), "res", StringComparison.Ordinal))
            {
                results.Add(json.RootElement.GetProperty("commandId").GetString()!);
            }
        }

        results.Contains("cmd-1").ShouldBeTrue();
        results.Contains("cancel-37").ShouldBeTrue();
        fixture.HighWater.ShouldBe(32);
    }

    private static async Task RejectForgeryAndCancelQueuedAsync(TestWebSocketSession session, Fixture fixture, CancelFirstHandler handler, CancellationToken cancellationToken)
    {
        using var wrongKey = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var forged = fixture.Signer.Sign(34, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "forged-34", nonce: "nonce-34", payloadJson: "{\"commandId\":\"cmd-1\"}", op: "command.cancel", signer: wrongKey);
        var deferredCancel = fixture.Signer.Sign(35, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cancel-35", nonce: "nonce-35", payloadJson: "{\"commandId\":\"cmd-33\"}", op: "command.cancel");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(forged)), cancellationToken).ConfigureAwait(false);
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(deferredCancel)), cancellationToken).ConfigureAwait(false);
        handler.Cancelled.Task.IsCompleted.ShouldBeFalse();
        var queued = fixture.Signer.Sign(36, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cancel-36", nonce: "nonce-36", payloadJson: "{\"commandId\":\"cmd-2\"}", op: "command.cancel");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(queued)), cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < 3; index++)
        {
            var ack = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            using var frame = JsonDocument.Parse(ack.Text);
            frame.RootElement.GetProperty("commandId").GetString().ShouldBe("cancel-36");
        }

        var result = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        using var completed = JsonDocument.Parse(result.Text);
        completed.RootElement.GetProperty("state").GetString().ShouldBe("completed");
        handler.Calls.ShouldBe(1);
    }

    private static async Task CompleteDeferredCommandAsync(TestWebSocketServer server, Fixture fixture, CancelFirstHandler handler, CancellationToken cancellationToken)
    {
        using var session = await server.AcceptAsync(cancellationToken).ConfigureAwait(false);
        var origin = new UriBuilder(server.Endpoint) { Scheme = "http" }.Uri.GetLeftPart(UriPartial.Authority);
        var challenge = new DispatchChallengeFrame { V = 1, T = DispatchFrameTypes.Challenge, Nonce = "reconnect-full", Origin = origin, ServerTime = Start.ToUnixTimeSeconds(), Protocols = ["1"] };
        await session.SendTextAsync(JsonSerializer.Serialize(challenge, DispatchJsonContext.Default.DispatchChallengeFrame), cancellationToken).ConfigureAwait(false);
        var message = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        using var hello = JsonDocument.Parse(message.Text);
        hello.RootElement.GetProperty("lastAppliedSeq").GetInt64().ShouldBe(32);
        var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 32 };
        await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cancellationToken).ConfigureAwait(false);
        var deferred = fixture.Signer.Sign(33, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-33", nonce: "nonce-redelivery");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(deferred)), cancellationToken).ConfigureAwait(false);
        await handler.DeferredStarted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var deferredCancel = fixture.Signer.Sign(35, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cancel-35", nonce: "nonce-35-retry", payloadJson: "{\"commandId\":\"cmd-33\"}", op: "command.cancel");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(deferredCancel)), cancellationToken).ConfigureAwait(false);
        await handler.DeferredCancelled.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var frame = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            using var json = JsonDocument.Parse(frame.Text);
            if (string.Equals(json.RootElement.GetProperty("t").GetString(), "res", StringComparison.Ordinal)
                && string.Equals(json.RootElement.GetProperty("commandId").GetString(), "cmd-33", StringComparison.Ordinal))
            {
                json.RootElement.GetProperty("state").GetString().ShouldBe("cancelled");
                break;
            }
        }

        var unknown = fixture.Signer.Sign(38, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "unknown-38", nonce: "nonce-38", payloadJson: "{\"commandId\":\"missing\"}", op: "command.cancel");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(unknown)), cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var frame = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            using var json = JsonDocument.Parse(frame.Text);
            if (string.Equals(json.RootElement.GetProperty("t").GetString(), "res", StringComparison.Ordinal)
                && string.Equals(json.RootElement.GetProperty("commandId").GetString(), "unknown-38", StringComparison.Ordinal))
            {
                json.RootElement.GetProperty("state").GetString().ShouldBe("failed");
                break;
            }
        }

        await session.SendTextAsync("{\"v\":1,\"t\":\"revoke\",\"reason\":\"revoked\"}", cancellationToken).ConfigureAwait(false);
    }


    [Fact]
    public async Task ATransientDisconnectPreservesRunningAndAcknowledgedQueuedCommands()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var handler = new ResumableHandler();
        var connector = fixture.BuildConnector(fixture.Pairing, [handler]);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connector.StateChanged += (_, _) =>
        {
            if (connector.State == DispatchConnectionState.Reconnecting)
            {
                disconnected.TrySetResult();
            }
        };
        var serverSide = Task.Run(async () =>
        {
            await DisconnectWithPendingCommandsAsync(server, fixture, handler, cts.Token).ConfigureAwait(false);
            await disconnected.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            handler.Release.TrySetResult();
            await handler.FirstFinished.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            await CompleteAfterReconnectAsync(server, fixture, cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        handler.Calls.ShouldBe(2);
    }

    private static async Task DisconnectWithPendingCommandsAsync(TestWebSocketServer server, Fixture fixture, ResumableHandler handler, CancellationToken cancellationToken)
    {
        using var session = await server.AcceptAsync(cancellationToken).ConfigureAwait(false);
        await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cancellationToken).ConfigureAwait(false);
        var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
        await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cancellationToken).ConfigureAwait(false);
        var first = fixture.Signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-running", nonce: "n-running");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(first)), cancellationToken).ConfigureAwait(false);
        await handler.Started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < 3; index++)
        {
            _ = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        var second = fixture.Signer.Sign(2, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-queued", nonce: "n-queued");
        await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(second)), cancellationToken).ConfigureAwait(false);
        var acknowledgement = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        using var ack = JsonDocument.Parse(acknowledgement.Text);
        ack.RootElement.GetProperty("state").GetString().ShouldBe("received");
        ack.RootElement.GetProperty("commandId").GetString().ShouldBe("cmd-queued");
        session.Abort();
    }

    private static async Task CompleteAfterReconnectAsync(TestWebSocketServer server, Fixture fixture, CancellationToken cancellationToken)
    {
        using var session = await server.AcceptAsync(cancellationToken).ConfigureAwait(false);
        var origin = new UriBuilder(server.Endpoint) { Scheme = "http" }.Uri.GetLeftPart(UriPartial.Authority);
        var challenge = new DispatchChallengeFrame { V = 1, T = DispatchFrameTypes.Challenge, Nonce = "reconnect123", Origin = origin, ServerTime = Start.ToUnixTimeSeconds(), Protocols = ["1"] };
        await session.SendTextAsync(JsonSerializer.Serialize(challenge, DispatchJsonContext.Default.DispatchChallengeFrame), cancellationToken).ConfigureAwait(false);
        var message = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        using var hello = JsonDocument.Parse(message.Text);
        hello.RootElement.GetProperty("lastAppliedSeq").GetInt64().ShouldBe(2);
        var running = hello.RootElement.GetProperty("running").EnumerateArray().Select(item => item.GetString()).ToArray();
        running.Contains("cmd-running", StringComparer.Ordinal).ShouldBeTrue();
        running.Contains("cmd-queued", StringComparer.Ordinal).ShouldBeTrue();
        var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 2 };
        await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cancellationToken).ConfigureAwait(false);
        var results = new HashSet<string>(StringComparer.Ordinal);
        while (results.Count < 2)
        {
            var frame = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            using var json = JsonDocument.Parse(frame.Text);
            if (string.Equals(json.RootElement.GetProperty("t").GetString(), "res", StringComparison.Ordinal))
            {
                json.RootElement.GetProperty("state").GetString().ShouldBe("completed");
                results.Add(json.RootElement.GetProperty("commandId").GetString()!);
            }
        }

        results.Contains("cmd-running").ShouldBeTrue();
        results.Contains("cmd-queued").ShouldBeTrue();
        await session.CloseOutputAsync((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked, "revoked", cancellationToken).ConfigureAwait(false);
        _ = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AHandlerFailureDoesNotStopTheNextCommand(bool unrelatedCancellation)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var server = new TestWebSocketServer();
        var fixture = await Fixture.CreateAsync(_directory, server.Endpoint, Start.AddHours(1), new FixedJitterSource(0.5), cts.Token);
        _disposables.Add(fixture);
        var handler = new ThrowOnceHandler(unrelatedCancellation);
        var connector = fixture.BuildConnector(fixture.Pairing, [handler]);
        var serverSide = Task.Run(async () =>
        {
            using var session = await server.AcceptAsync(cts.Token).ConfigureAwait(false);
            await ChallengeAsync(session, server.Endpoint, fixture.DevicePublicKey, cts.Token).ConfigureAwait(false);
            var welcome = new DispatchWelcomeFrame { V = 1, T = DispatchFrameTypes.Welcome, ServerTime = Start.ToUnixTimeSeconds(), HeartbeatSeconds = 20, Kids = [DispatchTestSigner.Kid], ResumeFromSeq = 0 };
            await session.SendTextAsync(JsonSerializer.Serialize(welcome, DispatchJsonContext.Default.DispatchWelcomeFrame), cts.Token).ConfigureAwait(false);
            var first = fixture.Signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-fails", nonce: "n-fails");
            var second = fixture.Signer.Sign(2, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, commandId: "cmd-succeeds", nonce: "n-succeeds");
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(first)), cts.Token).ConfigureAwait(false);
            await session.SendTextAsync(Encoding.UTF8.GetString(DispatchTestSigner.Wire(second)), cts.Token).ConfigureAwait(false);
            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            while (results.Count < 2)
            {
                var frame = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
                using var json = JsonDocument.Parse(frame.Text);
                if (string.Equals(json.RootElement.GetProperty("t").GetString(), "res", StringComparison.Ordinal))
                {
                    var id = json.RootElement.GetProperty("commandId").GetString()!;
                    results.Add(id, json.RootElement.GetProperty("state").GetString()!);
                    if (string.Equals(id, "cmd-fails", StringComparison.Ordinal))
                    {
                        json.RootElement.GetProperty("reason").GetString().ShouldBe("execution_error");
                    }
                }
            }

            results["cmd-fails"].ShouldBe("failed");
            results["cmd-succeeds"].ShouldBe("completed");
            await session.CloseOutputAsync((WebSocketCloseStatus)DispatchCloseCodes.DeviceRevoked, "revoked", cts.Token).ConfigureAwait(false);
            _ = await session.ReceiveAsync(cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        handler.Calls.ShouldBe(2);
    }

    private static async Task ChallengeAsync(TestWebSocketSession session, Uri endpoint, string devicePublicKey, CancellationToken cancellationToken)
    {
        var origin = "HTTP://" + endpoint.Authority + "/";
        var challenge = new DispatchChallengeFrame { V = 1, T = DispatchFrameTypes.Challenge, Nonce = "challenge123", Origin = origin, ServerTime = Start.ToUnixTimeSeconds(), Protocols = ["1"] };
        await session.SendTextAsync(JsonSerializer.Serialize(challenge, DispatchJsonContext.Default.DispatchChallengeFrame), cancellationToken).ConfigureAwait(false);
        var message = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        using var hello = JsonDocument.Parse(message.Text);
        hello.RootElement.GetProperty("deviceId").GetString().ShouldBe(DispatchTestSigner.DeviceId);
        var encoded = hello.RootElement.GetProperty("signature").GetString()!;
        var signature = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + "==");
        using var publicKey = System.Security.Cryptography.ECDsa.Create();
        publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(devicePublicKey), out _);
        publicKey.VerifyData(DispatchHelloCanonical.Build("challenge123", DispatchTestSigner.DeviceId, origin, 0), signature, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
    }

    /// <summary>A paired desktop on disk, with its secrets in memory and a connector built over them.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly DispatchTestSigner _signer;
        private readonly DispatchPolicyStore _policy;
        private readonly DispatchDedupeStore _dedupe;
        private readonly DispatchApprovalFlow _approvals;
        private readonly TimeProvider _clock;
        private readonly Uri _endpoint;
        private readonly IJitterSource _jitter;

        private Fixture(string directory, Uri endpoint, ISecretStore secrets, DispatchTestSigner signer, IJitterSource jitter, TimeProvider clock)
        {
            _signer = signer;
            _endpoint = endpoint;
            _jitter = jitter;
            _clock = clock;
            PairingPath = Path.Combine(directory, "pairing.json");
            Pairing = new DispatchPairing(secrets, PairingPath);
            _policy = new DispatchPolicyStore(Path.Combine(directory, "policy.json"));
            _dedupe = new DispatchDedupeStore(Path.Combine(directory, "dedupe.json"));
            _approvals = new DispatchApprovalFlow(new DenyingPrompt(), clock);
            Revocation = new RecordingRevocation();
            Connector = BuildConnector(Pairing);
        }

        public DispatchPairing Pairing { get; }

        public long HighWater => _dedupe.HighWater;

        public void Pause() => _policy.Save(_policy.Current with { Paused = true });

        public string DevicePublicKey { get; private set; } = string.Empty;

        public string PairingPath { get; }

        public RecordingRevocation Revocation { get; }

        public DispatchConnector Connector { get; }

        public static Task<Fixture> CreateAsync(string directory, Uri endpoint, DateTimeOffset credentialExpiry, IJitterSource jitter, CancellationToken cancellationToken) =>
            CreateWithClockAsync(directory, endpoint, credentialExpiry, jitter, new ManualTimeProvider(Start), cancellationToken);

        public static async Task<Fixture> CreateWithClockAsync(string directory, Uri endpoint, DateTimeOffset credentialExpiry, IJitterSource jitter, TimeProvider clock, CancellationToken cancellationToken)
        {
            _ = Directory.CreateDirectory(directory);
            var secrets = new InMemorySessionSecretStore();
            var signer = new DispatchTestSigner();
            var pairing = new DispatchPairing(secrets, Path.Combine(directory, "pairing.json"));
            var devicePublicKey = await pairing.EnrollDeviceKeyAsync(DispatchTestSigner.DeviceId, cancellationToken).ConfigureAwait(false);
            await pairing.CompleteAsync(
                DispatchTestSigner.DeviceId,
                new DispatchPairedDevice(DispatchTestSigner.DeviceId, DispatchTestSigner.AccountId, [signer.PublicKey]),
                new SecretString(DispatchTestSigner.Jwt(credentialExpiry)),
                cancellationToken).ConfigureAwait(false);
            return new Fixture(directory, endpoint, secrets, signer, jitter, clock) { DevicePublicKey = devicePublicKey };
        }

        public DispatchTestSigner Signer => _signer;

        public DispatchConnector BuildConnector(DispatchPairing pairing, IEnumerable<IDispatchCommandHandler>? handlers = null) => new(
            new DispatchConnectorOptions { Endpoint = _endpoint, ControllerName = "Controller" },
            pairing,
            _dedupe,
            _policy,
            _approvals,
            Revocation,
            handlers ?? Array.Empty<IDispatchCommandHandler>(),
            _clock,
            _jitter);

        public void Dispose()
        {
            _signer.Dispose();
        }
    }

    private sealed class RecordingRevocation : IDispatchRevocationSink
    {
        public int Calls { get; private set; }

        public Task EndDispatchedSessionsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingHandler : IDispatchCommandHandler
    {
        public int Calls { get; private set; }

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(DispatchOutcome.Completed("listed"));
        }
    }

    private sealed class ThrowOnceHandler(bool unrelatedCancellation) : IDispatchCommandHandler
    {
        public int Calls { get; private set; }

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
            {
                if (unrelatedCancellation)
                {
                    throw new OperationCanceledException("The handler cancelled its own operation.");
                }

                throw new IOException("The handler failed.");
            }

            return ValueTask.FromResult(DispatchOutcome.Completed("listed"));
        }
    }

    private sealed class HeartbeatClock(DateTimeOffset start) : TimeProvider
    {
        private readonly Autonogrammer.Accounts.Desktop.Tests.ManualTimeProvider _inner = new(start);

        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

        public override long GetTimestamp() => _inner.GetTimestamp();

        public override long TimestampFrequency => _inner.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _inner.CreateTimer(callback, state, dueTime, period);
            TimerCreated.TrySetResult();
            return timer;
        }

        public void Advance(TimeSpan by) => _inner.Advance(by);
    }

    private sealed class ResumableHandler : IDispatchCommandHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public async ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                FirstFinished.TrySetResult();
            }

            return DispatchOutcome.Completed("listed");
        }
    }

    private sealed class BlockingHandler : IDispatchCommandHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public async ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }

            return DispatchOutcome.Completed("unexpected");
        }
    }

    private sealed class CancelFirstHandler : IDispatchCommandHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource DeferredStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource DeferredCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public async ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
            {
                Started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Cancelled.TrySetResult();
                    throw;
                }
            }
            else if (string.Equals(command.CommandId, "cmd-33", StringComparison.Ordinal))
            {
                DeferredStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    DeferredCancelled.TrySetResult();
                    throw;
                }
            }

            return DispatchOutcome.Completed("listed");
        }
    }

    private sealed class DenyingPrompt : IDispatchApprovalPrompt
    {
        public ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DispatchApprovalChoice.Deny);
    }
}
