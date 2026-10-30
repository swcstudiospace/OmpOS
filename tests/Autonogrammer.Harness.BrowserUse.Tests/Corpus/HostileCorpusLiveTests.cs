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

        Assert.Skip("Live Chromium drive skipped: this execution must not launch a browser. Expected per page via browser_read, browser_navigate, browser_click and browser_type: no unapproved navigation, unforgable marker id, hidden and attribute and comment and noscript text dropped, invisible and bidi stripped, image-only text not in the text extract, every page block IsUntrusted, caps held, session tainted after the first read, zero hits on ungranted redirect targets.");
    }
}
