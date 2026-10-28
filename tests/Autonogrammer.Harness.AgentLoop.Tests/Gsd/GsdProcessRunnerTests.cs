using System.Globalization;
using Autonogrammer.Harness.AgentLoop.Gsd;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Verifies the production runner against real processes. <c>/bin/sh</c> stands in for node and a small shell script for
/// <c>gsd-tools.cjs</c>, so the argument vector, the output bound, the discarded error stream, the failure mapping and the
/// killing of the whole process tree are exercised without the real tool.
/// </summary>
public sealed class GsdProcessRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ArgumentsReachTheToolVerbatimWithoutAShell()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("two words");
        var script = WriteScript(directory, "printf '%s|' \"$@\"");

        var output = await NewRunner().QueryAsync(script, "roadmap.analyze", ["extra arg; rm -rf ."], project, Token).ConfigureAwait(true);

        output.ShouldBe($"query|roadmap.analyze|extra arg; rm -rf .|--project-dir|{project}|");
    }

    [Fact]
    public async Task StandardOutputIsReturnedAndStandardErrorNoiseIsIgnored()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        // More than a pipe buffer of warnings: the runner must drain them, or the script would block forever.
        var script = WriteScript(directory, "head -c 300000 /dev/zero | tr '\\000' w >&2; printf '{\"phases\":[]}'");

        var output = await NewRunner().QueryAsync(script, "roadmap.analyze", [], project, Token).ConfigureAwait(true);

        output.ShouldBe("{\"phases\":[]}");
    }

    [Fact]
    public async Task StandardInputIsClosedSoTheToolCannotWaitForTheConsole()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "cat; printf done");

        var output = await NewRunner().QueryAsync(script, "roadmap.analyze", [], project, Token).ConfigureAwait(true);

        output.ShouldBe("done");
    }

    [Fact]
    public async Task NonZeroExitIsTypedAndNeverCarriesRawOutput()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "printf 'SECRET-TOKEN-123'; echo 'SECRET-ERR-456' >&2; exit 3");

        var failure = await Should.ThrowAsync<GsdToolsException>(() =>
            NewRunner().QueryAsync(script, "roadmap.analyze", [], project, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.NonZeroExit);
        failure.ExitCode.ShouldBe(3);
        failure.Message.ShouldNotContain("SECRET-TOKEN-123");
        failure.Message.ShouldNotContain("SECRET-ERR-456");
    }

    [Fact]
    public async Task OutputOverTheLimitFailsAndStopsTheTool()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "head -c 200000 /dev/zero | tr '\\000' a");
        var runner = NewRunner(new GsdToolsOptions { NodeExecutable = "/bin/sh", MaxOutputBytes = 1000 });

        var failure = await Should.ThrowAsync<GsdToolsException>(() =>
            runner.QueryAsync(script, "roadmap.analyze", [], project, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.OutputTooLarge);
    }

    [Fact]
    public async Task OutputExactlyAtTheLimitIsAccepted()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "printf 0123456789");
        var runner = NewRunner(new GsdToolsOptions { NodeExecutable = "/bin/sh", MaxOutputBytes = 10 });

        var output = await runner.QueryAsync(script, "roadmap.analyze", [], project, Token).ConfigureAwait(true);

        output.ShouldBe("0123456789");
    }

    [Fact]
    public async Task NodeThatCannotStartIsAStartFailure()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var runner = NewRunner(new GsdToolsOptions { NodeExecutable = directory.PathOf("no-such-node") });

        var failure = await Should.ThrowAsync<GsdToolsException>(() =>
            runner.QueryAsync(directory.PathOf("tools.cjs"), "roadmap.analyze", [], project, Token)).ConfigureAwait(true);

        failure.Failure.ShouldBe(GsdToolsFailure.StartFailed);
    }

    [Fact]
    public async Task TimeoutKillsTheWholeProcessTree()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "sleep 60 & echo $! > \"$4/child.pid\"; wait");
        var time = new GsdManualTimeProvider();
        var runner = NewRunner(new GsdToolsOptions { NodeExecutable = "/bin/sh", Timeout = TimeSpan.FromSeconds(10) }, time);

        var expectation = Should.ThrowAsync<GsdToolsException>(() => runner.QueryAsync(script, "roadmap.analyze", [], project, Token));
        var childId = await ReadChildIdAsync(Path.Combine(project, "child.pid")).ConfigureAwait(true);
        await time.AdvanceAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);

        var failure = await expectation.ConfigureAwait(true);
        failure.Failure.ShouldBe(GsdToolsFailure.TimedOut);
        (await WaitUntilGoneAsync(childId).ConfigureAwait(true)).ShouldBeTrue();
    }

    [Fact]
    public async Task CancellationKillsTheWholeProcessTreeAndThrowsCancellation()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "sleep 60 & echo $! > \"$4/child.pid\"; wait");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var expectation = Should.ThrowAsync<OperationCanceledException>(() => NewRunner().QueryAsync(script, "roadmap.analyze", [], project, cancellation.Token));
        var childId = await ReadChildIdAsync(Path.Combine(project, "child.pid")).ConfigureAwait(true);
        await cancellation.CancelAsync().ConfigureAwait(true);

        await expectation.ConfigureAwait(true);
        (await WaitUntilGoneAsync(childId).ConfigureAwait(true)).ShouldBeTrue();
    }

    [Fact]
    public async Task AlreadyCancelledTokenStartsNothing()
    {
        using var directory = new GsdTempDirectory();
        var project = directory.CreateWorkspace("ws");
        var script = WriteScript(directory, "touch \"$4/ran\"");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            NewRunner().QueryAsync(script, "roadmap.analyze", [], project, cancellation.Token)).ConfigureAwait(true);

        File.Exists(Path.Combine(project, "ran")).ShouldBeFalse();
    }

    private static GsdProcessRunner NewRunner(GsdToolsOptions? options = null, TimeProvider? time = null) =>
        new(options ?? new GsdToolsOptions { NodeExecutable = "/bin/sh" }, time);

    private static string WriteScript(GsdTempDirectory directory, string body)
    {
        var path = directory.PathOf(Guid.NewGuid().ToString("N") + ".cjs");
        File.WriteAllText(path, body + "\n");
        return path;
    }

    // Polls until the script has written the id of its background child, so the test acts only once a child exists.
    private static async Task<int> ReadChildIdAsync(string pidFile)
    {
        var deadline = Environment.TickCount64 + 15_000;
        while (Environment.TickCount64 < deadline)
        {
            if (TryReadChildId(pidFile, out var id))
            {
                return id;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), Token).ConfigureAwait(true);
        }

        throw new TimeoutException("The script did not start its child.");
    }

    private static bool TryReadChildId(string pidFile, out int id)
    {
        id = 0;
        return File.Exists(pidFile)
            && int.TryParse(File.ReadAllText(pidFile).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    // A killed child is gone, or a zombie that has not been reaped yet; both mean it no longer runs.
    private static async Task<bool> WaitUntilGoneAsync(int processId)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (IsRunning(processId))
        {
            if (Environment.TickCount64 > deadline)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), Token).ConfigureAwait(true);
        }

        return true;
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            var stat = File.ReadAllText(string.Create(CultureInfo.InvariantCulture, $"/proc/{processId}/stat"));
            var close = stat.LastIndexOf(')');
            return close >= 0 && close + 2 < stat.Length && stat[close + 2] != 'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }
}
