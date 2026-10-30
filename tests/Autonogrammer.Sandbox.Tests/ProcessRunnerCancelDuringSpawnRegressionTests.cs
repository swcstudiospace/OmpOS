using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests: cancellation that lands before or while the child is starting must never leave
/// a running child or a descendant forked after the kill behind.
/// </summary>
public sealed class ProcessRunnerCancelDuringSpawnRegressionTests
{
    /// <summary>
    /// The child forks grandchildren in a tight loop, each of which writes its marker eight seconds after its fork.
    /// Cancelling mid-loop must stop every one of them, including those forked while the runner was still walking
    /// the tree to kill it. A grandchild that outlives the runner would write its marker after the runner returned.
    /// </summary>
    [Fact]
    public async Task CancelDuringSpawn_NoDescendantSurvives()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Cancellation test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await CommandProbe.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var dir = Path.Combine(Path.GetTempPath(), $"autonogrammer-spawn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var marker = Path.Combine(dir, "m");
            var spec = new ProcessSpec
            {
                Argv = ["/bin/sh", "-c", "i=0; while [ \"$i\" -lt 800 ]; do (sleep 8; touch \"$0.$i\") & i=$((i+1)); done; wait", marker],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Timeout = TimeSpan.FromSeconds(60),
            };

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cancel.CancelAfter(TimeSpan.FromMilliseconds(150));

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

            // Every grandchild writes its marker 8 seconds after its fork. Waiting past that point means any
            // grandchild the runner failed to kill has had time to write its marker.
            await Task.Delay(TimeSpan.FromSeconds(9), ct);
            Directory.GetFiles(dir).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A call whose token is already cancelled throws without launching the child, so its side effect never happens.
    /// </summary>
    [Fact]
    public async Task PreCancelledCall_NeverStartsTheChild()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Cancellation test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await CommandProbe.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var dir = Path.Combine(Path.GetTempPath(), $"autonogrammer-precancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var marker = Path.Combine(dir, "m");
            var spec = new ProcessSpec
            {
                Argv = ["/bin/sh", "-c", "touch \"$0\"", marker],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Timeout = TimeSpan.FromSeconds(60),
            };

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await cancel.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => new ProcessRunner().RunAsync(spec, cancel.Token));

            // Give a wrongly launched child time to run its touch before asserting it never did.
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            File.Exists(marker).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
