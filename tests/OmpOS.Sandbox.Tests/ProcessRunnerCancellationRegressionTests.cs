using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests: a caller cancellation terminates the child process tree instead of leaving it running.
/// </summary>
public sealed class ProcessRunnerCancellationRegressionTests
{
    /// <summary>
    /// A cancelled call kills the shell and its backgrounded subshell, so the delayed side effect never happens.
    /// </summary>
    [Fact]
    public async Task CancelledCall_TerminatesTheWholeChildTree()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Cancellation test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var marker = Path.Combine(Path.GetTempPath(), $"ompos-cancel-{Guid.NewGuid():N}");
        try
        {
            // The subshell is a grandchild of the runner's child. Only a tree-wide kill stops it before it writes the marker.
            var spec = new ProcessSpec
            {
                Argv = ["/bin/sh", "-c", "(sleep 2; touch \"$0\") & wait", marker],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Timeout = TimeSpan.FromSeconds(60),
            };

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cancel.CancelAfter(TimeSpan.FromMilliseconds(300));

            var canceled = false;
            try
            {
                await new ProcessRunner().RunAsync(spec, cancel.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            canceled.ShouldBeTrue();

            // Wait well past the subshell's sleep: a surviving child would have written the marker by now.
            await Task.Delay(TimeSpan.FromSeconds(4), ct);
            File.Exists(marker).ShouldBeFalse();
        }
        finally
        {
            if (File.Exists(marker))
            {
                File.Delete(marker);
            }
        }
    }
}
