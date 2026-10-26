using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests: a stale overlapping teardown must not release a re-reserved idempotency key.
/// </summary>
public sealed class PodmanOverlappingTeardownRegressionTests
{
    /// <summary>
    /// Two overlapping teardowns of one sandbox with a provision in between: the second (stale)
    /// teardown must leave the new reservation intact, so a later provision with the same key
    /// joins the new sandbox instead of creating a duplicate.
    /// </summary>
    [Fact]
    public async Task StaleOverlappingTeardown_DoesNotReleaseReprovisionedKey()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("0123456789ab"));

        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest();

        var first = await provider.ProvisionAsync(request, ct);
        first.NewlyProvisioned.ShouldBeTrue();

        // Park each `down` call so both teardowns capture the old sandbox before either finishes.
        var downCalls = 0;
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.PauseFor = spec =>
        {
            if (!PodmanTestData.HasArgument(spec, "down"))
            {
                return Task.CompletedTask;
            }

            return Interlocked.Increment(ref downCalls) == 1 ? releaseFirst.Task : releaseSecond.Task;
        };

        var teardownOne = provider.TeardownAsync(first.Descriptor.Id, ct);
        var teardownTwo = provider.TeardownAsync(first.Descriptor.Id, ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref downCalls) < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
        }

        downCalls.ShouldBe(2);

        // The first teardown finishes and releases the key; a new provision re-reserves it.
        releaseFirst.SetResult();
        await teardownOne.ConfigureAwait(true);

        var second = await provider.ProvisionAsync(request, ct).ConfigureAwait(true);
        second.NewlyProvisioned.ShouldBeTrue();
        second.Descriptor.Id.ShouldNotBe(first.Descriptor.Id, StringComparer.Ordinal);

        // The stale second teardown finishes: the new reservation must survive it.
        releaseSecond.SetResult();
        await teardownTwo.ConfigureAwait(true);

        var upsBefore = runner.Specs.Count(static s => PodmanTestData.HasArgument(s, "up"));
        var joined = await provider.ProvisionAsync(request, ct).ConfigureAwait(true);
        joined.NewlyProvisioned.ShouldBeFalse();
        joined.Descriptor.Id.ShouldBe(second.Descriptor.Id);
        runner.Specs.Count(static s => PodmanTestData.HasArgument(s, "up")).ShouldBe(upsBefore);

        await provider.TeardownAsync(second.Descriptor.Id, ct).ConfigureAwait(true);
    }
}
