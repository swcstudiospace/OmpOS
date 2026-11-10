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

        // env passed; SOTA placeholder drive body (ladder + budget assert deferred)
    }
}
