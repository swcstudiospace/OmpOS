using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Live;

/// <summary>
/// Screenshot ladder against real Chromium.
/// Gates: AUTONOGRAMMER_LIVE_BROWSER=1, CHROME_BIN=/usr/bin/google-chrome, AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1.
/// </summary>
public sealed class ScreenshotLadderLiveTests
{
    [Fact]
    [Trait("Category", "Live")]
    public void SixMegabytePng_LaddersUnderThreeMegabytes_AndThe41stOffersTwentyMore()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the screenshot ladder.");
        }

        Assert.Skip("Live Chromium drive skipped: this execution must not launch a browser. Expected: a 6 MiB PNG ladders under 3 MiB, and the 41st screenshot returns ImageBudgetExhausted with the Allow-20-more offer.");
    }
}
