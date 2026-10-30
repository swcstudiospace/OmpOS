using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

/// <summary>Locate order, version floor and the skipped snap path.</summary>
public sealed class ChromiumLocatorTests
{
    [Fact]
    public void Locate_PrefersExecutablePath_ThenManaged_ThenWellKnown()
    {
        var probe = new FakeChromiumProbe();
        probe.AddFile("/opt/configured/chrome", 153);
        probe.AddFile("/state/browser/chromium/153.0/chrome", 153);
        probe.AddDirectory("/state/browser/chromium", "153.0");
        probe.AddFile("/usr/bin/google-chrome", 153);
        var locator = new ChromiumLocator(probe);
        var options = new BrowserUseOptions { ExecutablePath = "/opt/configured/chrome" };

        locator.Locate(options, "/state", ChromiumOsFamily.Linux).ExecutablePath.ShouldBe("/opt/configured/chrome");

        options.ExecutablePath = string.Empty;
        locator.Locate(options, "/state", ChromiumOsFamily.Linux).ExecutablePath.ShouldBe("/state/browser/chromium/153.0/chrome");

        probe.ClearDirectories();
        locator.Locate(options, "/state", ChromiumOsFamily.Linux).ExecutablePath.ShouldBe("/usr/bin/google-chrome");
    }

    [Fact]
    public void Locate_Version148_IsTooOld_AndNothingFound_IsNotInstalled()
    {
        var probe = new FakeChromiumProbe();
        probe.AddFile("/usr/bin/google-chrome", 148);
        var locator = new ChromiumLocator(probe);
        var tooOld = locator.Locate(new BrowserUseOptions(), "/state", ChromiumOsFamily.Linux);
        tooOld.State.ShouldBe(BrowserLocateState.BrowserTooOld);

        probe.ClearFiles();
        locator.Locate(new BrowserUseOptions(), "/state", ChromiumOsFamily.Linux).State.ShouldBe(BrowserLocateState.BrowserNotInstalled);
    }

    [Fact]
    public void Locate_SkipsSnapChromium()
    {
        var probe = new FakeChromiumProbe();
        probe.AddFile("/snap/bin/chromium", 160);
        probe.AddFile("/usr/bin/chromium", 160);
        probe.AddLink("/usr/bin/chromium", "/snap/chromium/current/usr/lib/chromium-browser/chrome");
        var locator = new ChromiumLocator(probe);
        locator.Locate(new BrowserUseOptions(), "/state", ChromiumOsFamily.Linux).State.ShouldBe(BrowserLocateState.BrowserNotInstalled);
    }
}
