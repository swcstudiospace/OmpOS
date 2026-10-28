using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>The in-memory host the other cloud tests stand on: it must behave like a service, or those tests prove nothing.</summary>
public sealed class FakeCloudAgentsHostTests
{
    private readonly ManualTimeProvider _clock = new();

    private static CloudAgentRequest Request(string name = "Fix the build") => new()
    {
        Name = name,
        Repositories = [CloudFixtures.Repo()],
        IdleMinutes = 10,
    };

    /// <summary>A created agent is listed, starts provisioning with a session and its first history step, and counts in the limits.</summary>
    [Fact]
    public async Task ACreatedAgentIsListedAndCounted()
    {
        var host = new FakeCloudAgentsHost(_clock) { MaxAgents = 3 };
        var token = TestContext.Current.CancellationToken;

        var agent = await host.CreateAsync(Request(), token).ConfigureAwait(true);

        agent.Status.ShouldBe(CloudAgentStatus.Provisioning);
        agent.SessionId.ShouldNotBeNullOrEmpty();
        agent.Timeline.ShouldHaveSingleItem().Status.ShouldBe(CloudAgentStatus.Provisioning);
        (await host.ListAsync(token).ConfigureAwait(true)).ShouldHaveSingleItem().Id.ShouldBe(agent.Id);
        (await host.GetLimitsAsync(token).ConfigureAwait(true)).ShouldBe(new CloudLimits(1, 3, 0, null));
    }

    /// <summary>Agents get distinct identifiers.</summary>
    [Fact]
    public async Task AgentsGetDistinctIdentifiers()
    {
        var host = new FakeCloudAgentsHost(_clock);
        var token = TestContext.Current.CancellationToken;

        var first = await host.CreateAsync(Request("one"), token).ConfigureAwait(true);
        var second = await host.CreateAsync(Request("two"), token).ConfigureAwait(true);

        first.Id.ShouldNotBe(second.Id, StringComparer.Ordinal);
        first.SessionId.ShouldNotBe(second.SessionId, StringComparer.Ordinal);
    }

    /// <summary>A create over the cap is refused the way a service refuses it.</summary>
    [Fact]
    public async Task ACreateOverTheCapIsRefused()
    {
        var host = new FakeCloudAgentsHost(_clock) { MaxAgents = 1 };
        var token = TestContext.Current.CancellationToken;
        _ = await host.CreateAsync(Request(), token).ConfigureAwait(true);

        var refusal = await Should.ThrowAsync<CloudHostException>(() => host.CreateAsync(Request("two"), token)).ConfigureAwait(true);

        refusal.Kind.ShouldBe(CloudFailureKind.LimitReached);
        host.Agents.Count.ShouldBe(1);
    }

    /// <summary>Wake and Stop move the status and extend the history; an unknown agent is not found.</summary>
    [Fact]
    public async Task WakeAndStopMoveTheStatus()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Stopped));
        var token = TestContext.Current.CancellationToken;

        var woken = await host.WakeAsync("a", token).ConfigureAwait(true);
        var stopped = await host.StopAsync("a", token).ConfigureAwait(true);

        woken.Status.ShouldBe(CloudAgentStatus.Starting);
        stopped.Status.ShouldBe(CloudAgentStatus.Stopped);
        stopped.Timeline.Count.ShouldBe(4);
        var missing = await Should.ThrowAsync<CloudHostException>(() => host.WakeAsync("nobody", token)).ConfigureAwait(true);
        missing.Kind.ShouldBe(CloudFailureKind.NotFound);
    }

    /// <summary>A scripted failure is used once and the next call works.</summary>
    [Fact]
    public async Task AScriptedFailureIsUsedOnce()
    {
        var host = new FakeCloudAgentsHost(_clock);
        var token = TestContext.Current.CancellationToken;
        host.FailNext("List", new CloudHostException(CloudFailureKind.Unavailable, "down"));

        await Should.ThrowAsync<CloudHostException>(() => host.ListAsync(token)).ConfigureAwait(true);

        (await host.ListAsync(token).ConfigureAwait(true)).ShouldBeEmpty();
        host.CountOf("List").ShouldBe(2);
    }

    /// <summary>A held call waits for its release and a cancelled wait ends the call.</summary>
    [Fact]
    public async Task AHeldCallWaitsForItsReleaseAndCanBeCancelled()
    {
        var host = new FakeCloudAgentsHost(_clock);
        var release = host.HoldNext("List");

        var pending = host.ListAsync(TestContext.Current.CancellationToken);
        pending.IsCompleted.ShouldBeFalse();
        release();
        (await pending.ConfigureAwait(true)).ShouldBeEmpty();

        _ = host.HoldNext("List");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancelled = host.ListAsync(cancel.Token);
        await cancel.CancelAsync().ConfigureAwait(true);
        var ended = false;
        try
        {
            _ = await cancelled.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ended = true;
        }

        ended.ShouldBeTrue("a cancelled wait ends the call with a cancellation");
    }

    /// <summary>Raising the change signal reaches every subscriber.</summary>
    [Fact]
    public void RaisingChangesReachesTheSubscribers()
    {
        var host = new FakeCloudAgentsHost(_clock);
        var heard = 0;
        host.Changes += (_, _) => heard++;

        host.PublishChanges();
        host.PublishChanges();

        heard.ShouldBe(2);
    }
}
