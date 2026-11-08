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
