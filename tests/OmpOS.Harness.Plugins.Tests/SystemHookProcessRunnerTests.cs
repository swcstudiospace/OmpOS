using System.ComponentModel;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Verifies the production runner against real processes: argument handling without a shell, the explicit working
/// directory, exit code and output capture, closed standard input, the output bound, and killing the whole process tree.
/// </summary>
public sealed class SystemHookProcessRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ArgumentsReachTheProcessVerbatimWithoutAShell()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand(["/bin/sh", "-c", "printf %s \"$1\"", "sh", "two words; rm -rf ."], directory.Path);

        var outcome = await new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.ShouldBe("two words; rm -rf .");
    }

    [Fact]
    public async Task ProcessStartsInTheCommandsWorkingDirectory()
    {
        using var directory = new TempDirectory();
        var workingDirectory = directory.PathOf("hook-cwd");
        Directory.CreateDirectory(workingDirectory);
        var command = new HookCommand(["/bin/sh", "-c", "basename \"$(pwd -P)\""], workingDirectory);

        var outcome = await new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token);

        outcome.StandardOutput.Trim().ShouldBe("hook-cwd");
    }

    [Fact]
    public async Task ExitCodeAndBothStreamsAreCaptured()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand(["/bin/sh", "-c", "echo out; echo err 1>&2; exit 3"], directory.Path);

        var outcome = await new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token);

        outcome.ExitCode.ShouldBe(3);
        outcome.StandardOutput.ShouldBe("out\n");
        outcome.StandardError.ShouldBe("err\n");
        outcome.StandardOutputTruncated.ShouldBeFalse();
        outcome.StandardErrorTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task StandardInputIsClosedSoAHookCannotWaitForTheConsole()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand(["/bin/sh", "-c", "cat; echo done"], directory.Path);

        var outcome = await new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.ShouldBe("done\n");
    }

    [Fact]
    public async Task OutputIsKeptUpToTheBoundAndFlaggedWhenCut()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand(["/bin/sh", "-c", "head -c 40000 /dev/zero | tr '\\000' a"], directory.Path);

        var outcome = await new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.Length.ShouldBe(HookOutput.MaxBytes);
        outcome.StandardOutputTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task CancellationKillsTheWholeProcessTree()
    {
        using var directory = new TempDirectory();
        var ready = directory.PathOf("grandchild-ready");
        var finished = directory.PathOf("grandchild-finished");
        // The shell starts a background subshell, which announces itself and then sleeps before it would write the
        // finished marker. The run is cancelled only once the grandchild has announced itself.
        var command = new HookCommand(
            ["/bin/sh", "-c", "(touch \"$1\"; sleep 3; touch \"$2\") & wait", "sh", ready, finished],
            directory.Path);

        await Should.ThrowAsync<OperationCanceledException>(() => RunUntilGrandchildStartsThenCancelAsync(command, ready));

        await Task.Delay(TimeSpan.FromSeconds(4), Token);
        File.Exists(finished).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutableThatCannotStartFailsToStart()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand([directory.PathOf("no-such-hook")], directory.Path);

        await Should.ThrowAsync<Win32Exception>(() =>
            new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, Token));
    }

    // Starts the command, waits until its grandchild has written the ready marker, then cancels the run. The run's
    // cancellation is what the caller observes as OperationCanceledException.
    private static async Task RunUntilGrandchildStartsThenCancelAsync(HookCommand command, string ready)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = new SystemHookProcessRunner().RunAsync(command, HookOutput.MaxBytes, cancellation.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(ready) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
        }

        File.Exists(ready).ShouldBeTrue();
        await cancellation.CancelAsync().ConfigureAwait(false);
        await running.ConfigureAwait(false);
    }
}
