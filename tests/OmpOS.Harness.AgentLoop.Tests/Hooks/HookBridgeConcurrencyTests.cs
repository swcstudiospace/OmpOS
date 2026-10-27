using System.Globalization;
using OmpOS.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies that requests are served concurrently, each answered with its own decision, that the concurrency bound neither
/// deadlocks nor leaks a slot, and that a client that stalls halfway through its request blocks nobody else.
/// </summary>
public sealed class HookBridgeConcurrencyTests
{
    private const int InFlight = 16;
    private static readonly TimeSpan Safety = TimeSpan.FromSeconds(30);

    private static string Name(int index) => "evt-" + index.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task SixteenGatedRequestsAreInFlightAtTheSameTimeAndEachGetsItsOwnAnswer()
    {
        var entered = 0;
        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeBridgeHandler(async (request, cancellationToken) =>
        {
            if (Interlocked.Increment(ref entered) == InFlight)
            {
                allEntered.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            return new BridgeDecision { AdditionalContext = request.Event };
        });
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = Enumerable.Range(0, InFlight).Select(index => rig.PostAsync("{\"event\":\"" + Name(index) + "\"}")).ToList();

        await allEntered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        pending.Count(static task => task.IsCompleted).ShouldBe(0);
        release.SetResult();
        var responses = await Task.WhenAll(pending).ConfigureAwait(true);

        responses.Length.ShouldBe(InFlight);
        for (var index = 0; index < InFlight; index++)
        {
            responses[index].Status.ShouldBe(200);
            responses[index].Json().GetProperty("additional_context").GetString().ShouldBe(Name(index));
        }

        handler.Calls.ShouldBe(InFlight);
    }

    [Fact]
    public async Task WithOneSlotEveryRequestStillCompletesAndNeverTwoRunTogether()
    {
        const int requests = 8;
        var running = 0;
        var highest = 0;
        var handler = new FakeBridgeHandler(async (_, _) =>
        {
            var now = Interlocked.Increment(ref running);
            int seen;
            do
            {
                seen = Volatile.Read(ref highest);
            }
            while (now > seen && Interlocked.CompareExchange(ref highest, now, seen) != seen);

            await Task.Yield();
            Interlocked.Decrement(ref running);
            return new BridgeDecision();
        });
        var rig = await HookBridgeRig.StartAsync(handler, new HookBridgeOptions { MaxConcurrentRequests = 1 }).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(index => rig.PostAsync("{\"event\":\"" + Name(index) + "\"}"))).ConfigureAwait(true);

        responses.ShouldAllBe(static response => response.Status == 200);
        handler.Calls.ShouldBe(requests);
        Volatile.Read(ref highest).ShouldBe(1);
    }

    [Fact]
    public async Task AClientThatStallsInTheMiddleOfItsHeadBlocksNobodyElseAndDoesNotBlockDisposal()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision { HooksRun = 1 });
        var options = new HookBridgeOptions { ReadTimeout = TimeSpan.FromHours(1) };
        var rig = await HookBridgeRig.StartAsync(handler, options).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        using var stalled = await rig.ConnectAsync().ConfigureAwait(true);
        await stalled.GetStream().WriteAsync("POST /v1/hook HTTP/1.1\r\nAuthorization: Bearer "u8.ToArray(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(index => rig.PostAsync("{\"event\":\"" + Name(index) + "\"}"))).ConfigureAwait(true);
        await rig.DisposeAsync().ConfigureAwait(true);

        responses.ShouldAllBe(static response => response.Status == 200);
        handler.Calls.ShouldBe(4);
    }
}
