using Autonogrammer.Harness.BrowserUse.Vision;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Vision;

/// <summary>The screenshot ladder uses CDP parameters only.</summary>
public sealed class ScreenshotLadderTests
{
    [Fact]
    public async Task SixMegabytePng_LaddersUnderThreeMegabytes()
    {
        var pipeline = new ScreenshotPipeline(delay: static (_, _) => Task.CompletedTask);
        var shot = await pipeline.CaptureAsync(CaptureAsync, 3 * 1024 * 1024, "desktop", CancellationToken.None).ConfigureAwait(true);
        shot.Failure.ShouldBeNull();
        shot.Bytes.Length.ShouldBeLessThan(3 * 1024 * 1024);
        shot.Steps.Select(step => (step.Format, step.Quality, step.Scale)).ShouldBe([("png", null, 1d), ("jpeg", 85, 1d), ("jpeg", 70, 1d), ("jpeg", 70, 0.5d)]);
        shot.MimeType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task Zoom_StaysFullResolution_WithinTheCap()
    {
        var pipeline = new ScreenshotPipeline(delay: static (_, _) => Task.CompletedTask);
        var shot = await pipeline.ZoomAsync(static (step, _) => Task.FromResult(step.Zoom && step.Scale == 1 ? new byte[100] : new byte[4 * 1024 * 1024]), 3 * 1024 * 1024, CancellationToken.None).ConfigureAwait(true);
        shot.Bytes.Length.ShouldBe(100);
        shot.Steps.Single().Scale.ShouldBe(1);
    }

    [Fact]
    public async Task Shots_WaitAtLeast500Milliseconds_AndThe41stFailsUntilTwentyMore()
    {
        TimeSpan waited = TimeSpan.Zero;
        var time = new FixedTime();
        var pipeline = new ScreenshotPipeline(time, (span, _) =>
        {
            waited = span;
            return Task.CompletedTask;
        });
        _ = await pipeline.CaptureAsync(static (_, _) => Task.FromResult(new byte[1]), 10, "tablet", CancellationToken.None).ConfigureAwait(true);
        time.Advance(TimeSpan.FromMilliseconds(100));
        _ = await pipeline.CaptureAsync(static (_, _) => Task.FromResult(new byte[1]), 10, "mobile", CancellationToken.None).ConfigureAwait(true);
        waited.ShouldBe(TimeSpan.FromMilliseconds(400));
        var limits = new BrowserLimits(new Options.BrowserUseOptions());
        for (var i = 0; i < 40; i++)
        {
            limits.TryRecordShot().ShouldBeTrue();
        }

        limits.TryRecordShot().ShouldBeFalse();
        limits.AllowTwentyMore();
        limits.TryRecordShot().ShouldBeTrue();
    }

    private static Task<byte[]> CaptureAsync(ScreenshotStep step, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var png = string.Equals(step.Format, "png", StringComparison.Ordinal);
        var size = step.Scale < 1 ? 1024 : png ? 6 * 1024 * 1024 : step.Quality == 85 ? 4 * 1024 * 1024 : (4 * 1024 * 1024) - 1;
        return Task.FromResult(new byte[size]);
    }

    private sealed class FixedTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
