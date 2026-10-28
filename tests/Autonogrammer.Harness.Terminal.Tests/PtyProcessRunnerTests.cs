using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Verifies the pseudo-terminal runner against real processes: the argument list, the working directory, exit codes,
/// streamed and bounded output, closed input, terminal size, cancellation and start failure.
/// The command is <c>/bin/sh</c>, which every Linux test host has; the arguments are passed as a list, never as a string.
/// </summary>
public sealed class PtyProcessRunnerTests
{
    private const string Shell = "/bin/sh";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunsARealCommandFromAnArgumentListAndReturnsOutputAndExitCode()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();

        var outcome = await runner.RunAsync(Sh(workspace.Path, "printf hello"), Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.ShouldBe("hello");
        outcome.StandardError.ShouldBeEmpty();
        outcome.StandardOutputTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportsTheExitCodeOfAFailingCommand()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();

        var outcome = await runner.RunAsync(Sh(workspace.Path, "printf failed; exit 3"), Token);

        outcome.ExitCode.ShouldBe(3);
        outcome.StandardOutput.ShouldBe("failed");
    }

    [Fact]
    public async Task PassesEachArgumentUnchangedAndNeverInterpretsItsContents()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();
        var request = new ProcessRequest(
            Shell,
            ["-c", "printf '%s|' \"$1\" \"$2\"", "sh", "a b", "$HOME;x"],
            workspace.Path,
            4096);

        var outcome = await runner.RunAsync(request, Token);

        outcome.StandardOutput.ShouldBe("a b|$HOME;x|");
    }

    [Fact]
    public async Task RunsInTheExplicitWorkingDirectory()
    {
        using var workspace = new TempWorkspace();
        var inner = workspace.CreateSubdirectory("inner");
        var runner = new PtyProcessRunner();

        var outcome = await runner.RunAsync(Sh(inner, "pwd -P"), Token);

        outcome.StandardOutput.Trim().ShouldEndWith(Path.Combine("inner"));
    }

    [Fact]
    public async Task StreamsOutputToTheSinkAndReturnsTheSameText()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var runner = new PtyProcessRunner(sink);

        var outcome = await runner.RunAsync(Sh(workspace.Path, "printf one; printf two"), Token);

        sink.Text.ShouldBe("onetwo");
        outcome.StandardOutput.ShouldBe("onetwo");
    }

    [Fact]
    public async Task KeepsAndStreamsOnlyTheConfiguredNumberOfCharacters()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var runner = new PtyProcessRunner(sink);

        var outcome = await runner.RunAsync(
            new ProcessRequest(Shell, ["-c", "head -c 100000 /dev/zero | tr '\\0' a"], workspace.Path, 100),
            Token);

        outcome.StandardOutput.Length.ShouldBe(100);
        outcome.StandardOutputTruncated.ShouldBeTrue();
        sink.Text.Length.ShouldBe(100);
    }

    [Fact]
    public async Task ClosesStandardInputSoACommandThatReadsItDoesNotHang()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();

        var outcome = await runner.RunAsync(Sh(workspace.Path, "cat; printf done"), Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.ShouldBe("done");
    }

    [Fact]
    public async Task StartsTheCommandAtTheSizeSetBeforeTheRun()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();
        runner.Resize(100, 40);

        var outcome = await runner.RunAsync(Sh(workspace.Path, "stty size"), Token);

        outcome.StandardOutput.Trim().ShouldBe("40 100");
    }

    [Fact]
    public async Task ResizesARunInProgressWhenTheSizeChanges()
    {
        using var workspace = new TempWorkspace();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new RecordingSink(text =>
        {
            if (text.Contains("ready", StringComparison.Ordinal))
            {
                ready.TrySetResult();
            }
        });
        var runner = new PtyProcessRunner(sink);

        var run = runner.RunAsync(Sh(workspace.Path, "printf ready; sleep 1; stty size"), Token);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), Token);
        runner.Resize(100, 40);
        var outcome = await run.ConfigureAwait(true);

        outcome.StandardOutput.ShouldContain("40 100");
    }

    [Fact]
    public async Task CancellingKillsTheProcessAndThrows()
    {
        using var workspace = new TempWorkspace();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new RecordingSink(text =>
        {
            if (text.Contains("started", StringComparison.Ordinal))
            {
                started.TrySetResult();
            }
        });
        var runner = new PtyProcessRunner(sink);

        var run = runner.RunAsync(Sh(workspace.Path, "printf started; exec sleep 30"), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20), Token);
        var clock = Stopwatch.StartNew();
        await cancellation.CancelAsync();

        var cancelled = false;
        try
        {
            await run.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        cancelled.ShouldBeTrue("a cancelled run must throw OperationCanceledException");
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task AMissingExecutableIsReportedAsAnExitWithNoOutput()
    {
        using var workspace = new TempWorkspace();
        var runner = new PtyProcessRunner();

        // Porta.Pty does not report an exec failure as a spawn exception. The child exits with code 2 (ENOENT) and prints nothing.
        var outcome = await runner.RunAsync(
            new ProcessRequest(Path.Combine(workspace.Path, "autonogrammer-no-such-command"), [], workspace.Path, 4096),
            Token);

        outcome.ExitCode.ShouldBe(2);
        outcome.StandardOutput.ShouldBeEmpty();
    }

    [Fact]
    public void ResizeRejectsANonPositiveSize()
    {
        var runner = new PtyProcessRunner();

        Should.Throw<ArgumentOutOfRangeException>(() => runner.Resize(0, 24));
        Should.Throw<ArgumentOutOfRangeException>(() => runner.Resize(80, 0));
    }

    private static ProcessRequest Sh(string workingDirectory, string script)
    {
        return new ProcessRequest(Shell, ["-c", script], workingDirectory, 4096);
    }
}
