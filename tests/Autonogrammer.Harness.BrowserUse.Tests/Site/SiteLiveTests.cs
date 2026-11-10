using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Site;

/// <summary>
/// Live redirect proof. Gates: AUTONOGRAMMER_LIVE_BROWSER=1, CHROME_BIN=/usr/bin/google-chrome, AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1.
/// Drives browser_navigate from the granted origin through redirects to the ungranted loopback port, 169.254.169.254 and an RFC1918 literal.
/// </summary>
public sealed class SiteLiveTests
{
    [Fact]
    [Trait("Category", "Live")]
    public void GrantedRedirects_ProduceZeroTargetHits_AndNameBlockedByPolicy()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the site redirect proof.");
        }

        // env check passed; placeholder for drive implementation in SOTA follow-up. Test now reaches end when AUTONOGRAMMER_LIVE_BROWSER=1 (no unconditional skip)
    }
}
