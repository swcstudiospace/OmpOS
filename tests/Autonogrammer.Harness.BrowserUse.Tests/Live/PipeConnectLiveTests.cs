using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Live;

/// <summary>
/// The pipe-connect spike is live and skipped unless <c>AUTONOGRAMMER_LIVE_BROWSER=1</c>.
/// Lifetime bounds are proved offline with a fake launcher and a manual clock.
/// </summary>
public sealed class PipeConnectLiveTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task Pipe_ConnectsOverTheHostTransport_AndScrubsTheEnvironment()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_BROWSER"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_BROWSER=1 to run the pipe-connect spike.");
        }

        var chrome = Environment.GetEnvironmentVariable("CHROME_BIN");
        if (string.IsNullOrEmpty(chrome))
        {
            Assert.Skip("Set CHROME_BIN to the Chromium binary.");
        }

        var planted = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OMP_API_KEY"] = "planted-not-a-secret-1",
            ["FOO_TOKEN"] = "planted-not-a-secret-2",
            ["BAR_SECRET"] = "planted-not-a-secret-3",
            ["AUTONOGRAMMER_X"] = "planted-not-a-secret-4",
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
            ["HOME"] = Environment.GetEnvironmentVariable("HOME"),
            ["LANG"] = "C.UTF-8",
        };
        var host = new BrowserSessionHost(new ChromiumProcessLauncher(), new BrowserUseOptions());
        try
        {
            var session = await host.OpenAsync(chrome, 9, planted, TestContext.Current.CancellationToken).ConfigureAwait(true);
            try
            {
                var version = await session.GetVersionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                Major(version).ShouldBeGreaterThanOrEqualTo(149);
                await session.CaptureOneFrameAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                var environ = await File.ReadAllTextAsync("/proc/" + session.ProcessId + "/environ", TestContext.Current.CancellationToken).ConfigureAwait(true);
                environ.ShouldNotContain("planted-not-a-secret-");
                environ.ShouldNotContain("OMP_API_KEY");
            }
            finally
            {
                await session.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SessionHost_EnforcesBounds_AndWipesTheProfile()
    {
        var launcher = new FakeChromiumLauncher();
        var now = DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions(), () => now);
        try
        {
            var parent = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin", ["OMP_API_KEY"] = "planted-not-a-secret-1" };
            var first = await host.OpenAsync("/usr/bin/google-chrome", 9, parent, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var second = await host.OpenAsync("/usr/bin/google-chrome", 9, parent, TestContext.Current.CancellationToken).ConfigureAwait(true);
            var concurrent = await Should.ThrowAsync<BrowserSessionLimitException>(() => host.OpenAsync("/usr/bin/google-chrome", 9, parent, TestContext.Current.CancellationToken)).ConfigureAwait(true);
            concurrent.Limit.ShouldBe(BrowserSessionLimit.ConcurrentBrowsers);
            (first.ProfileMode & UnixFileMode.UserRead).ShouldBe(UnixFileMode.UserRead);
            (first.ProfileMode & UnixFileMode.GroupRead).ShouldBe(UnixFileMode.None);

            for (var tab = 0; tab < 5; tab++)
            {
                await first.OpenTabAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            }

            var tabs = await Should.ThrowAsync<BrowserSessionLimitException>(() => first.OpenTabAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
            tabs.Limit.ShouldBe(BrowserSessionLimit.Tabs);
            launcher.SessionAt(1).WorkingSetBytes = (1536L * 1024 * 1024) + 1;
            var memory = await Should.ThrowAsync<BrowserSessionLimitException>(() => second.OpenTabAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
            memory.Limit.ShouldBe(BrowserSessionLimit.Memory);
            Directory.Exists(second.ProfileDirectory).ShouldBeFalse();
            now = now.AddMinutes(11);
            (await host.EnforceBoundsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(1);
            Directory.Exists(first.ProfileDirectory).ShouldBeFalse();
            launcher.SessionAt(0).CdpOrder.ShouldBe(1);
            launcher.SessionAt(0).KillOrder.ShouldBe(2);
            now = DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            var third = await host.OpenAsync("/usr/bin/google-chrome", 9, parent, TestContext.Current.CancellationToken).ConfigureAwait(true);
            now = now.AddHours(3);
            (await host.EnforceBoundsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(1);
            Directory.Exists(third.ProfileDirectory).ShouldBeFalse();
            launcher.RequestAt(0).Environment.ContainsKey("OMP_API_KEY").ShouldBeFalse();
            launcher.RequestAt(0).Arguments.ShouldNotContain(argument => argument.Contains("--no-sandbox", StringComparison.Ordinal));
            launcher.RequestAt(0).Arguments.ShouldNotContain(argument => argument.Contains("--remote-debugging-port", StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static int Major(string version)
    {
        var digits = new string(version.SkipWhile(static character => !char.IsDigit(character)).TakeWhile(static character => char.IsDigit(character)).ToArray());
        return int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
    }
}
