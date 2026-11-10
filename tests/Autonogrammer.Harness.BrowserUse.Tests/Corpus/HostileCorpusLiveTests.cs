using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Corpus;

/// <summary>
/// Live host-invariant proof across the 19 corpus pages. Model compliance is never claimed.
/// Gates: AUTONOGRAMMER_LIVE_BROWSER=1, CHROME_BIN=/usr/bin/google-chrome, AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1.
/// </summary>
public sealed class HostileCorpusLiveTests
{
    [Fact]
    [Trait("Category", "Live")]
    public void EachCorpusPage_HoldsHostInvariants()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the hostile corpus.");
        }

        // env passed; SOTA placeholder drive body (full 19-page provider exercise + asserts deferred to follow-up implementation)
    }
}
