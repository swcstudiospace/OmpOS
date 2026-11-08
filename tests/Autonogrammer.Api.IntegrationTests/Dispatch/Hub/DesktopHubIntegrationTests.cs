using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Dispatch.Desktop;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Exercises the real desktop connector against the relay over loopback WebSockets.</summary>
public sealed class DesktopHubIntegrationTests
{
    /// <summary>Running and acknowledged queued work survive a transient disconnect and both complete after reconnect.</summary>
    [Fact]
    public async Task DesktopPairsReconnectsAndCompletesRunningAndQueuedCommands()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        var directory = Path.Combine(Path.GetTempPath(), "dispatch-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var host = await DispatchApiHost.StartAsync(ct, network: true).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            host.Services.GetRequiredService<IOptions<DispatchOptions>>().Value.HubOrigin = host.Origin.AbsoluteUri;
            using var owner = DispatchTestClient.As(host);
            var pairing = new DispatchPairing(new InMemorySessionSecretStore(), Path.Combine(directory, "pairing.json"));
            var publicKey = await pairing.EnrollDeviceKeyAsync("pending", ct).ConfigureAwait(true);
            using var deviceKey = ECDsa.Create();
            deviceKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            var point = deviceKey.ExportParameters(false).Q;
            var body = "{\"name\":\"Desk\",\"platform\":\"linux\",\"appVersion\":\"1\",\"protocolVersions\":[\"1\"],\"capabilities\":[{\"id\":\"sessions\",\"version\":1,\"enabled\":true}],\"publicKey\":{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + Base64UrlEncoder.Encode(point.X) + "\",\"y\":\"" + Base64UrlEncoder.Encode(point.Y) + "\"}}";
            var reply = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices", body, ct).ConfigureAwait(true);
            reply.Status.ShouldBe(HttpStatusCode.Created);
            using var response = JsonDocument.Parse(reply.Body);
            var root = response.RootElement;
            var deviceId = root.GetProperty("deviceId").GetString()!;
            var verification = root.GetProperty("verificationKeys")[0];
            var jwk = verification.GetProperty("jwk");
            using var hubKey = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("x").GetString()!), Y = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("y").GetString()!) },
            });
            var hubPublicKey = new DispatchPublicKey(verification.GetProperty("kid").GetString()!, Convert.ToBase64String(hubKey.ExportSubjectPublicKeyInfo()));
            await pairing.CompleteAsync("pending", new DispatchPairedDevice(deviceId, DispatchTestClient.Owner, [hubPublicKey]), new SecretString(root.GetProperty("credential").GetString()!), ct).ConfigureAwait(true);
            var endpoint = new UriBuilder(host.Origin) { Scheme = "ws", Path = "/dispatch/devices/" + deviceId + "/connect" }.Uri;
            var handler = new SessionHandler();
            var connector = new DispatchConnector(new DispatchConnectorOptions { Endpoint = endpoint, ControllerName = "Controller" }, pairing,
                new DispatchDedupeStore(Path.Combine(directory, "dedupe.json")), new DispatchPolicyStore(Path.Combine(directory, "policy.json")),
                new DispatchApprovalFlow(new DenyPrompt(), TimeProvider.System), new Revocation(), [handler], TimeProvider.System, new Jitter());
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var running = connector.RunAsync(stop.Token);
            try
            {
                await ExerciseReconnectAsync(host, owner, deviceId, handler, ct).ConfigureAwait(true);
            }
            finally
            {
                await stop.CancelAsync().ConfigureAwait(true);
                try { await running.ConfigureAwait(true); } catch (OperationCanceledException) { }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task ExerciseReconnectAsync(DispatchApiHost host, HttpClient owner, string deviceId, SessionHandler handler, CancellationToken cancellationToken)
    {
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var first = await EnqueueAsync(owner, deviceId, cancellationToken).ConfigureAwait(true);
        await handler.Started.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        var second = await EnqueueAsync(owner, deviceId, cancellationToken).ConfigureAwait(true);
        await WaitForStateAsync(store, second, DispatchCommandState.Received, cancellationToken).ConfigureAwait(true);
        var sessions = host.Services.GetRequiredService<DispatchSessionRegistry>();
        var previous = sessions.ForDevice(deviceId).Single();
        await previous.CloseAsync(DispatchCloseCodes.GoingAway, "test reconnect", cancellationToken).ConfigureAwait(true);
        while (!sessions.ForDevice(deviceId).Any(session => !ReferenceEquals(session, previous)))
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(true);
        }

        handler.Release.TrySetResult();
        await WaitForStateAsync(store, first, DispatchCommandState.Completed, cancellationToken).ConfigureAwait(true);
        await WaitForStateAsync(store, second, DispatchCommandState.Completed, cancellationToken).ConfigureAwait(true);
        handler.Calls.ShouldBe(2);
    }

    private static async Task<string> EnqueueAsync(HttpClient owner, string deviceId, CancellationToken cancellationToken)
    {
        var command = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), cancellationToken).ConfigureAwait(true);
        command.Status.ShouldBe(HttpStatusCode.Accepted);
        return HubTestSockets.Field(command.Body, "commandId");
    }

    private static async Task WaitForStateAsync(IDispatchStore store, string commandId, DispatchCommandState state, CancellationToken cancellationToken)
    {
        while ((await store.GetCommandAsync(DispatchTestClient.Owner, commandId, cancellationToken).ConfigureAwait(true))?.State != state)
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(true);
        }
    }

    private sealed class DenyPrompt : IDispatchApprovalPrompt
    {
        public ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(DispatchApprovalChoice.Deny);
    }

    private sealed class Revocation : IDispatchRevocationSink
    {
        public Task EndDispatchedSessionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Jitter : IJitterSource
    {
        public double NextDouble() => 0.5;
    }

    private sealed class SessionHandler : IDispatchCommandHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public bool CanHandle(DispatchOp op) => op == DispatchOp.SessionList;

        public async ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return DispatchOutcome.Completed("listed");
        }
    }
}
