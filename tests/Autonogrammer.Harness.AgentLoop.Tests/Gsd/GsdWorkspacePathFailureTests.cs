using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Gsd;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// A workspace path the operating system cannot resolve ends the phase stage and the task context as plain failures, never as
/// exceptions: the observer reports a degraded stage and the context leaves the phase out with a warning.
/// </summary>
public sealed class GsdWorkspacePathFailureTests
{
    private const string UnresolvableRoot = "bad\0root";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheObserverReportsADegradedStageInsteadOfThrowing()
    {
        using var directory = new GsdTempDirectory();
        var runner = new GsdFakeProcessRunner();
        var time = new GsdManualTimeProvider();
        var observer = new GsdStateObserver(GsdTestSupport.NewClient(directory, runner, time), time);

        var enumerator = observer.ObserveAsync(GsdTestSupport.Scope(UnresolvableRoot), Token).GetAsyncEnumerator(Token);
        try
        {
            (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            enumerator.Current.Status.ShouldBe(StageStatus.Degraded);
            enumerator.Current.Detail.ShouldNotBeNull().ShouldStartWith("Workflow state could not be read: ");
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        runner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task TheTaskContextLeavesThePhaseOutWithAWarningInsteadOfThrowing()
    {
        using var directory = new GsdTempDirectory();
        var source = new GsdTaskContextSource(GsdTestSupport.NewClient(directory, new GsdFakeProcessRunner()));

        var context = await source.GetAsync(GsdTestSupport.Scope(UnresolvableRoot), Token).ConfigureAwait(true);

        context.Phase.ShouldBeNull();
        context.Title.ShouldBe("Untitled task");
        context.Warnings.ShouldHaveSingleItem().ShouldStartWith("Workflow phase is unknown: the read failed (");
    }
}
