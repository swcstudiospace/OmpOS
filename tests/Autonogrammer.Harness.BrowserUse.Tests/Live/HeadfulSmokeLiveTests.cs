using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Shouldly;
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
    public async Task Screencast_Launches_ConnectsThePipe_GetsVersion_AndScrubsTheEnvironment()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the headful smoke.");
        }

        var chrome = Environment.GetEnvironmentVariable("CHROME_BIN");
        if (string.IsNullOrEmpty(chrome))
        {
            Assert.Skip("Set CHROME_BIN to the Chromium binary.");
        }

        // The caller supplies a private xvfb-run display; this test never starts or attaches to one.
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        display.ShouldNotBeNullOrEmpty("Run this test under a private xvfb-run -a display.");
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
            ["HOME"] = Environment.GetEnvironmentVariable("HOME"),
            ["DISPLAY"] = display,
            ["LANG"] = "C.UTF-8",
            ["OMP_API_KEY"] = "planted-not-a-secret-headful",
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        IChromiumLauncher launcher = string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX"), "1", StringComparison.Ordinal)
            ? new ExplicitNoSandboxTestLauncher()
            : new ChromiumProcessLauncher();
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        await using var hostDisposal = host.ConfigureAwait(true);
        var session = await host.OpenAsync(chrome, 9, environment, timeout.Token).ConfigureAwait(true);
        await using var sessionDisposal = session.ConfigureAwait(true);
        (await session.GetVersionAsync(timeout.Token).ConfigureAwait(true)).ShouldNotBeNullOrEmpty();
        await session.CaptureOneFrameAsync(timeout.Token).ConfigureAwait(true);
        if (OperatingSystem.IsLinux())
        {
            var environ = await File.ReadAllTextAsync("/proc/" + session.ProcessId + "/environ", timeout.Token).ConfigureAwait(true);
            environ.ShouldNotContain("planted-not-a-secret-headful");
            environ.ShouldNotContain("OMP_API_KEY");
            var arguments = await File.ReadAllTextAsync("/proc/" + session.ProcessId + "/cmdline", timeout.Token).ConfigureAwait(true);
            arguments.ShouldContain("--headless=new");
        }
    }

    // Explicit test-only opt-in for disposable Chromium on root-run CI; production never uses this wrapper.
    private sealed class ExplicitNoSandboxTestLauncher : IChromiumLauncher
    {
        public Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken) =>
            new ChromiumProcessLauncher().LaunchAsync(
                request with { Arguments = [.. request.Arguments, "--no-sandbox"] },
                cancellationToken);
    }
}
