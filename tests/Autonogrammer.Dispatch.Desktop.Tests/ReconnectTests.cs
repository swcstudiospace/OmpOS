using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Dispatch.Desktop.Tests.Support;
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
            await session.SendTextAsync("{\"v\":1,\"t\":\"revoke\",\"reason\":\"revoked\"}", cts.Token).ConfigureAwait(false);
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        }, cts.Token);
        await connector.RunAsync(cts.Token);
        await serverSide.ConfigureAwait(true);
        connector.State.ShouldBe(DispatchConnectionState.DeviceRevoked);
        fixture.Revocation.Calls.ShouldBe(1);
    }

    private static async Task ChallengeAsync(TestWebSocketSession session, Uri endpoint, string devicePublicKey, CancellationToken cancellationToken)
    {
        var origin = new UriBuilder(endpoint) { Scheme = "http" }.Uri.GetLeftPart(UriPartial.Authority);
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

    private sealed class DenyingPrompt : IDispatchApprovalPrompt
    {
        public ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DispatchApprovalChoice.Deny);
    }
}
