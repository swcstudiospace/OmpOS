using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests: followed container logs are delivered as they arrive, not after the log process exits.
/// </summary>
public sealed class PodmanFollowedLogsRegressionTests
{
    /// <summary>
    /// A followed line reaches the consumer while the log stream is still open.
    /// </summary>
    [Fact]
    public async Task FollowedLogs_LineIsDeliveredWhileTheStreamIsStillOpen()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("container-id-123"));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var provisioned = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);

        // The second line is held back until the test has consumed the first one.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.StreamFor = _ => GatedLines(release.Task);

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(TimeSpan.FromSeconds(10));
        await using var lines = provider
            .StreamLogsAsync(provisioned.Descriptor.Id, new LogCursor { Position = 0, Follow = true }, bounded.Token)
            .GetAsyncEnumerator(bounded.Token);

        (await lines.MoveNextAsync()).ShouldBeTrue();
        lines.Current.ShouldBe("first");

        release.SetResult();
        (await lines.MoveNextAsync()).ShouldBeTrue();
        lines.Current.ShouldBe("second");

        runner.Specs.ShouldContain(static s => PodmanTestData.HasArgument(s, "-f"));
    }

    private static async IAsyncEnumerable<string> GatedLines(Task release)
    {
        yield return "first";
        await release;
        yield return "second";
    }
}
