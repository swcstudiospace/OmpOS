using Autonogrammer.Harness.BrowserUse.Chromium;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

/// <summary>The one flag list, including the absence of a debugging port and a sandbox escape.</summary>
public sealed class ChromiumLaunchPlanTests
{
    [Fact]
    public void Build_IncludesEveryIsolationFlagExactlyOnce()
    {
        var flags = ChromiumLaunchPlan.Build(9, 1280, 800, "/tmp/ag-profile");
        string[] required =
        [
            "--headless=new",
            "--remote-debugging-pipe",
            "--user-data-dir=/tmp/ag-profile",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-sync",
            "--disable-background-networking",
            "--disable-component-update",
            "--mute-audio",
            "--hide-scrollbars",
            "--password-store=basic",
            "--use-mock-keychain",
            "--proxy-server=http://127.0.0.1:9",
            "--proxy-bypass-list=<-loopback>",
            "--force-webrtc-ip-handling-policy=disable_non_proxied_udp",
            "--window-size=1280,800",
        ];

        foreach (var flag in required)
        {
            flags.Count(candidate => string.Equals(candidate, flag, StringComparison.Ordinal)).ShouldBe(1);
        }

        flags.ShouldNotContain(flag => flag.Contains("--remote-debugging-port", StringComparison.Ordinal));
        flags.ShouldNotContain(flag => flag.Contains("--no-sandbox", StringComparison.Ordinal));
        flags.Count.ShouldBe(required.Length);
    }
}
