using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Tests.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the read timeout: a client that does not finish its request in time is disconnected without an answer and never
/// reaches the handler, and a client that is on time is not cut short. The clock is fake, so nothing waits for time to pass.
/// </summary>
public sealed class HookBridgeSlowClientTests
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(7);

    private static HookBridgeOptions Options() => new() { ReadTimeout = ReadTimeout };

    [Fact]
    public async Task AClientThatSendsPartOfItsHeadAndStopsIsDisconnectedWithoutAnAnswerWhenTheReadTimeoutElapses()
    {
        var clock = new StageFakeTimeProvider();
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, Options(), clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using var client = await rig.ConnectAsync().ConfigureAwait(true);
        var stream = client.GetStream();
        await stream.WriteAsync("POST /v1/hook HTTP/1.1\r\nHost: x\r\n"u8.ToArray(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);

        clock.Advance(ReadTimeout);
        var response = await HookBridgeTestResponse.ReadAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.Status.ShouldBe(0);
        response.Body.ShouldBeEmpty();
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AClientThatSendsAHeadAndOnlyPartOfItsBodyIsDisconnectedWithoutAnAnswerAndTheHandlerNeverRuns()
    {
        var clock = new StageFakeTimeProvider();
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler, Options(), clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using var client = await rig.ConnectAsync().ConfigureAwait(true);
        var stream = client.GetStream();
        var request = HookBridgeTestRequest.Raw(
            "POST /v1/hook HTTP/1.1\r\nHost: x\r\nAuthorization: " + rig.Authorization + "\r\nContent-Length: 100\r\n",
            "{\"event\":");
        await stream.WriteAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);

        clock.Advance(ReadTimeout);
        var response = await HookBridgeTestResponse.ReadAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.Status.ShouldBe(0);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ARequestThatCompletesJustBeforeTheReadTimeoutIsServed()
    {
        var clock = new StageFakeTimeProvider();
        var handler = FakeBridgeHandler.Returning(new BridgeDecision { HooksRun = 1 });
        var rig = await HookBridgeRig.StartAsync(handler, Options(), clock: clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using var client = await rig.ConnectAsync().ConfigureAwait(true);
        var stream = client.GetStream();
        var request = HookBridgeTestRequest.Post("{\"event\":\"stop\"}", rig.Authorization);
        await stream.WriteAsync(request.AsMemory(0, 10), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);

        clock.Advance(ReadTimeout - TimeSpan.FromMilliseconds(1));
        await stream.WriteAsync(request.AsMemory(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var response = await HookBridgeTestResponse.ReadAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        handler.Calls.ShouldBe(1);
    }
}
