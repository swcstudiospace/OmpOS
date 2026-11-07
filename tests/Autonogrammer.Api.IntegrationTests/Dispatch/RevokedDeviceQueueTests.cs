using System.Net;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Once a device is revoked, the queue the hub delivers from returns none of its commands, so a racing enqueue cannot reach it.</summary>
public sealed class RevokedDeviceQueueTests
{
    /// <summary>A queued command is listed for a live device and not for a revoked one.</summary>
    [Fact]
    public async Task A_revoked_device_has_no_queued_commands_for_the_hub_to_deliver()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var accepted = await DispatchTestClient.SendAsync(client, HttpMethod.Post, "/dispatch/devices/" + deviceId + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        accepted.Status.ShouldBe(HttpStatusCode.Accepted);
        var store = host.Services.GetRequiredService<IDispatchStore>();
        (await store.ListQueuedAfterAsync("acct", deviceId, 0, ct).ConfigureAwait(true)).Count.ShouldBe(1);

        _ = await store.RevokeDeviceAsync("acct", deviceId, "revoked", TimeProvider.System.GetUtcNow(), ct).ConfigureAwait(true);

        (await store.ListQueuedAfterAsync("acct", deviceId, 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
    }
}
