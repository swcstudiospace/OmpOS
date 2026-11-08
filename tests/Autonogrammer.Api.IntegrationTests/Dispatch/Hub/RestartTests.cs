using System.Security.Cryptography;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A device that reconnects without a command it was running fails that command with device_restarted before the welcome, so the welcome never precedes the settled state (D-27).</summary>
public sealed class RestartTests
{
    /// <summary>A running command the hello does not list is failed with device_restarted.</summary>
    [Fact]
    public async Task An_in_flight_command_the_hello_does_not_list_fails_device_restarted_before_the_welcome()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, device.DeviceId, "in-flight", "restart-key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddMinutes(5)), ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, DispatchTestClient.Owner, "in-flight", ct).ConfigureAwait(true);

        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");

        var row = (await store.ListCommandsAsync(DispatchTestClient.Owner, device.DeviceId, 10, ct).ConfigureAwait(true)).Single();
        row.State.ShouldBe(DispatchCommandState.Failed);
        row.Reason.ShouldBe(DispatchFailureReason.DeviceRestarted);
    }

    /// <summary>Restart reconciliation reaches in-flight commands older than the first 100 rows.</summary>
    [Fact]
    public async Task A_hello_settles_all_in_flight_commands_across_pages()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        var now = DateTimeOffset.UtcNow;
        for (var index = 1; index <= 101; index++)
        {
            var id = "restart-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            (await store.EnqueueAsync(new EnqueueDispatchCommand(DispatchTestClient.Owner, device.DeviceId, id, "key-" + id, "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", now, now.AddMinutes(5)), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
            await MoveToRunningAsync(store, DispatchTestClient.Owner, id, ct).ConfigureAwait(true);
        }

        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        (await HubTestSockets.HelloAsync(socket, device, 0, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        (await store.GetCommandAsync(DispatchTestClient.Owner, "restart-1", ct).ConfigureAwait(true))!.Reason.ShouldBe(DispatchFailureReason.DeviceRestarted);
        (await store.GetCommandAsync(DispatchTestClient.Owner, "restart-101", ct).ConfigureAwait(true))!.Reason.ShouldBe(DispatchFailureReason.DeviceRestarted);
        (await store.ListInFlightAfterAsync(DispatchTestClient.Owner, device.DeviceId, 0, 100, ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>A hello accepts 32 outstanding IDs and rejects a larger list.</summary>
    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public async Task A_hello_enforces_the_running_id_limit(int count, bool accepted)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = bound.Token;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await HubTestSockets.PairAsync(host, key, ct).ConfigureAwait(true);
        using var socket = await HubTestSockets.ConnectAsync(host, device, ct).ConfigureAwait(true);
        var challenge = await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true);
        var nonce = challenge.GetProperty("nonce").GetString()!;
        var origin = challenge.GetProperty("origin").GetString()!;
        var canonical = DispatchHelloCanonical.Build(nonce, device.DeviceId, origin, 0);
        var signature = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var hello = new DispatchHelloFrame { V = 1, T = DispatchFrameTypes.Hello, DeviceId = device.DeviceId, Nonce = nonce, Origin = origin, LastAppliedSeq = 0, Signature = signature, Running = Enumerable.Range(0, count).Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray() };
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(hello, DispatchJsonContext.Default.DispatchHelloFrame);
        await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(true);
        if (accepted)
        {
            (await HubTestSockets.ReceiveJsonAsync(socket, ct).ConfigureAwait(true)).GetProperty("t").GetString().ShouldBe("welcome");
        }
        else
        {
            (await HubTestSockets.ReceiveCloseAsync(socket, ct).ConfigureAwait(true)).ShouldBe((System.Net.WebSockets.WebSocketCloseStatus)DispatchCloseCodes.InvalidPayload);
        }
    }

    private static async Task MoveToRunningAsync(IDispatchStore store, string accountId, string commandId, CancellationToken cancellationToken)
    {
        (DispatchCommandState From, DispatchCommandState To)[] path =
        [
            (DispatchCommandState.Queued, DispatchCommandState.Delivered),
            (DispatchCommandState.Delivered, DispatchCommandState.Received),
            (DispatchCommandState.Received, DispatchCommandState.Accepted),
            (DispatchCommandState.Accepted, DispatchCommandState.Running),
        ];
        foreach (var (from, to) in path)
        {
            (await store.TryTransitionAsync(accountId, commandId, from, to, null, null, cancellationToken).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }
    }
}
