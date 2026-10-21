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
        using var release = new SemaphoreSlim(0);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(TimeSpan.FromSeconds(10));
        runner.StreamFor = _ => GatedLinesAsync(release, bounded.Token);
        await using var lines = provider
            .StreamLogsAsync(provisioned.Descriptor.Id, new LogCursor { Position = 0, Follow = true }, bounded.Token)
            .GetAsyncEnumerator(bounded.Token);

        (await lines.MoveNextAsync()).ShouldBeTrue();
        lines.Current.ShouldBe("first");

        release.Release();
        (await lines.MoveNextAsync()).ShouldBeTrue();
        lines.Current.ShouldBe("second");

        runner.Specs.ShouldContain(static s => PodmanTestData.HasArgument(s, "-f"));
    }

    private static async IAsyncEnumerable<string> GatedLinesAsync(SemaphoreSlim release, CancellationToken cancellationToken)
    {
        yield return "first";
        await release.WaitAsync(cancellationToken).ConfigureAwait(true);
        yield return "second";
    }
}
