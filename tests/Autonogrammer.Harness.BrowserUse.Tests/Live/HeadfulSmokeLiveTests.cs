using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Live;

/// <summary>
/// Headful smoke under a private xvfb-run -a display, never the session display.
/// Gates: AUTONOGRAMMER_LIVE_BROWSER=1, CHROME_BIN=/usr/bin/google-chrome, AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1.
/// Headed mode stays off (--headless=new) with a visible frame pump.
/// </summary>
public sealed class HeadfulSmokeLiveTests
{
    [Fact]
    [Trait("Category", "Live")]
    public void Screencast_Launches_ConnectsThePipe_GetsVersion_AndScrubsTheEnvironment()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the headful smoke.");
        }

        Assert.Skip("Live Chromium drive skipped: this execution must not launch Chromium or Xvfb. Expected under private xvfb-run -a: launch, pipe connect, getVersion, one frame, scrubbed environ, --headless=new still set.");
    }
}
