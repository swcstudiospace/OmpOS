using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Fakes;
using Autonogrammer.Harness.ComputerUse.Frames;
using Autonogrammer.Harness.ComputerUse.Tests.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Frames;

/// <summary>Token math, the 80-image cap and zoom retention.</summary>
public sealed class ImageBudgetTests
{
    [Fact]
    public void TokensMatchCeilFormulaAnd1280By720IsAbout1200()
    {
        VisionPipeline.Tokens(1280, 720).ShouldBe(1196);
        Math.Abs(VisionPipeline.Tokens(1280, 720) - 1200).ShouldBeLessThanOrEqualTo(5);
        VisionPipeline.Tokens(28, 28).ShouldBe(1);
        VisionPipeline.Tokens(29, 1).ShouldBe(2);
    }

    [Fact]
    public async Task EightyFirstImageIsImageBudgetAndZoomDropsAfterSixtySeconds()
    {
        ManualTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        ComputerUseOptions options = new() { MaxImagesPerSession = 80, SettleMs = 0, MaxImageBytes = 3_145_728 };
        FakeComputerUseBackend backend = new();
        backend.SetPointer(new PointerState { X = 1, Y = 2, Buttons = 0 });
        VisionPipeline pipeline = new(options, backend, time, modelId: null);
        DisplayInfo display = new()
        {
            Id = "d",
            Bounds = new PixelRect { X = 0, Y = 0, Width = 1, Height = 1 },
            ScaleFactor = 1,
            Primary = true,
        };
        for (int i = 0; i < 80; i++)
        {
            VisionCapture capture = await pipeline.CaptureAsync("s", display, [], new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);
            capture.Denial.ShouldBeNull();
            capture.Frame!.Png.Length.ShouldBeGreaterThan(0);
        }

        VisionCapture denied = await pipeline.CaptureAsync("s", display, [], new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);
        denied.Denial.ShouldBe(DenialCode.ImageBudget);
        denied.Frame.ShouldBeNull();
        pipeline.LatestForZoom("s").ShouldNotBeNull();
        time.Advance(TimeSpan.FromSeconds(60));
        pipeline.LatestForZoom("s").ShouldBeNull();
    }
}
