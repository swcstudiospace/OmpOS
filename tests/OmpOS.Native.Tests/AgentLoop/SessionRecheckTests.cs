using System.Collections.Concurrent;
using System.Net;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Agents;
using Shouldly;
using Xunit;
using static OmpOS.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// Re-checking a plugin stage of a session: after the operator fixed the plugin (ran the git command the stage names), the check
/// resolves the plugins again and publishes what it found into the session's own stage stream.
/// </summary>
public sealed class SessionRecheckTests
{
    /// <summary>A plugin that was unavailable and is now installed is reported Succeeded, with the honest note that it loads at the next start of the agent.</summary>
    [Fact]
    public async Task ARecheckAfterTheFixShowsThePluginReady()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var seen = new ConcurrentQueue<StageEvent>();
        using var observing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var observation = Task.Run(
            async () =>
            {
                await foreach (var stageEvent in slot.Stages.ObserveAsync(slot.Scope.Current, observing.Token).WithCancellation(observing.Token).ConfigureAwait(false))
                {
                    seen.Enqueue(stageEvent);
                }
            },
            CancellationToken.None);
        await EventuallyAsync(
            () => seen.Any(static e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Unavailable }),
            "the plugin to be reported unavailable",
            ct).ConfigureAwait(true);

        tree.WriteReadyPlugins();
        await slot.RecheckAsync(StageSource.Ultrathink, ct).ConfigureAwait(true);

        await EventuallyAsync(
            () => seen.Any(static e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Succeeded }),
            "the plugin to be reported ready",
            ct).ConfigureAwait(true);
        var ready = seen.Last(static e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Succeeded });
        ready.Detail.ShouldNotBeNull().ShouldContain("loads the next time this agent starts");
        ready.SessionId.ShouldBe(slot.Id);
        await observing.CancelAsync().ConfigureAwait(true);
        await observation.ConfigureAwait(true);
    }

    /// <summary>A plugin that is still missing is reported with the same fix, and a source that is not a plugin is refused.</summary>
    [Fact]
    public async Task AMissingPluginStaysUnavailableAndTheEngineIsNotAPlugin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var enumerator = plan.Recheck.ObserveAsync(slot.Scope.Current, reading.Token).GetAsyncEnumerator(reading.Token);
        await using var enumeratorLifetime = enumerator.ConfigureAwait(true);

        await slot.RecheckAsync(StageSource.Swarm, ct).ConfigureAwait(true);

        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        enumerator.Current.Status.ShouldBe(StageStatus.Unavailable);
        enumerator.Current.Detail.ShouldNotBeNull().ShouldContain("git submodule update --init --recursive plugins/agent-swarm");
        await Should.ThrowAsync<InvalidOperationException>(() => slot.RecheckAsync(StageSource.Omp, ct)).ConfigureAwait(true);
    }

    /// <summary>Re-checking the Substrate asks it again: a Substrate that answers now is reported Succeeded.</summary>
    [Fact]
    public async Task ARecheckOfTheSubstrateAsksItAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler { HealthStatus = HttpStatusCode.ServiceUnavailable };
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), name => string.Equals(name, AgentLoopEnvironment.SubstrateUrlVariable, StringComparison.Ordinal) ? "http://127.0.0.1:7410" : null, substrate));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);
        plan.SubstrateActive.ShouldBeFalse();
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var enumerator = plan.Recheck.ObserveAsync(slot.Scope.Current, reading.Token).GetAsyncEnumerator(reading.Token);
        await using var enumeratorLifetime = enumerator.ConfigureAwait(true);

        await slot.RecheckAsync(StageSource.Substrate, ct).ConfigureAwait(true);
        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        enumerator.Current.Status.ShouldBe(StageStatus.Degraded);

        substrate.HealthStatus = HttpStatusCode.OK;
        await slot.RecheckAsync(StageSource.Substrate, ct).ConfigureAwait(true);
        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        enumerator.Current.Status.ShouldBe(StageStatus.Succeeded);
        enumerator.Current.Detail.ShouldNotBeNull().ShouldContain("The Substrate answers");
    }
}
