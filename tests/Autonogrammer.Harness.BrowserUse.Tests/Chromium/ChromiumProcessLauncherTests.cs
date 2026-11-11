using System.Diagnostics;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

public sealed class ChromiumProcessLauncherTests
{
    [Fact]
    public async Task CancellationDuringPipeHandshakeTerminatesTheChild()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The stalled child fixture uses /bin/sh on Linux.");
        }

        var directory = Directory.CreateTempSubdirectory("ag-browser-launch-test-").FullName;
        var pidPath = Path.Combine(directory, "pid");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var request = new ChromiumLaunchRequest(
                "/bin/sh",
                ["-c", "echo $$ > \"$PIDFILE\"; exec sleep 60"],
                new Dictionary<string, string>(StringComparer.Ordinal) { ["PIDFILE"] = pidPath, ["PATH"] = "/usr/bin:/bin" },
                directory);
            var launch = new ChromiumProcessLauncher().LaunchAsync(request, cancellation.Token);
            while (!File.Exists(pidPath))
            {
                await Task.Delay(20, cancellation.Token).ConfigureAwait(true);
            }

            var pid = int.Parse(await File.ReadAllTextAsync(pidPath, cancellation.Token).ConfigureAwait(true), System.Globalization.CultureInfo.InvariantCulture);
            using var child = Process.GetProcessById(pid);
            await cancellation.CancelAsync().ConfigureAwait(true);
            try
            {
                _ = await launch.ConfigureAwait(true);
                Assert.Fail("A stalled pipe handshake must not complete after cancellation.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Expected: cancellation stops the handshake.
            }
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(exitTimeout.Token).ConfigureAwait(true);
            child.HasExited.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
