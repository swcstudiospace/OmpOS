using System.Globalization;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Terminal.Tests;

/// <summary>
/// Verifies the interactive Linux backend against real pseudo-terminals: typed input reaches the command, its output and
/// exit code reach the caller, the working directory and size apply, the command sees only the allowlisted environment,
/// disposal ends a running process, and unsafe or invalid options are refused before a process starts. The command is
/// <c>/bin/sh</c>, as in the one-shot runner tests; every argument goes in a list.
/// </summary>
public sealed class LinuxLocalPtyBackendTests
{
    private const string Shell = "/bin/sh";
    private const string SentinelName = "OMPOS_TERMINAL_TEST_SENTINEL";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EchoesTypedInputAndDeliversTheMarkerAndTheExitCode()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var backend = new LinuxLocalPtyBackend();

        var session = await backend.StartAsync(
            new TerminalSessionOptions(
                Shell,
                ["-c", "read line; printf 'MARK<%s>\\n' \"$line\"; exit 7"],
                workspace.Path,
                80,
                24,
                sink),
            Token).ConfigureAwait(true);
        try
        {
            await session.WriteInputAsync("typed\n", Token).ConfigureAwait(true);
            var exitCode = await session.WaitForExitAsync(Token).ConfigureAwait(true);

            exitCode.ShouldBe(7);
            sink.Text.ShouldContain("typed");
            sink.Text.ShouldContain("MARK<typed>");
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ASizeSetBeforeTheInputIsTheSizeTheCommandSees()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();

        var session = await new LinuxLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(Shell, ["-c", "read line; stty size; exit 0"], workspace.Path, 80, 24, sink),
            Token).ConfigureAwait(true);
        try
        {
            session.Resize(100, 40);
            await session.WriteInputAsync("go\n", Token).ConfigureAwait(true);
            var exitCode = await session.WaitForExitAsync(Token).ConfigureAwait(true);

            exitCode.ShouldBe(0);
            sink.Text.ShouldContain("40 100");
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task StartsTheCommandInTheExplicitWorkingDirectory()
    {
        using var workspace = new TempWorkspace();
        var inner = workspace.CreateSubdirectory("inner");
        var sink = new RecordingSink();

        var session = await new LinuxLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(Shell, ["-c", "read line; pwd -P; exit 0"], inner, 80, 24, sink),
            Token).ConfigureAwait(true);
        try
        {
            await session.WriteInputAsync("\n", Token).ConfigureAwait(true);
            await session.WaitForExitAsync(Token).ConfigureAwait(true);

            var directory = sink.Text.Split('\n', StringSplitOptions.TrimEntries).Single(line => line.StartsWith('/'));
            directory.ShouldBe(PhysicalPath(inner));
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task RefusesAnExecutableThatIsNotAnAbsolutePath()
    {
        using var workspace = new TempWorkspace();
        var backend = new LinuxLocalPtyBackend();

        await Should.ThrowAsync<ArgumentException>(() => backend.StartAsync(
            new TerminalSessionOptions("sh", ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink()),
            Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task RefusesAWorkingDirectoryThatDoesNotExist()
    {
        using var workspace = new TempWorkspace();
        var backend = new LinuxLocalPtyBackend();
        var missing = Path.Combine(workspace.Path, "no-such-directory");

        await Should.ThrowAsync<DirectoryNotFoundException>(() => backend.StartAsync(
            new TerminalSessionOptions(Shell, ["-c", "exit 0"], missing, 80, 24, new RecordingSink()),
            Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task RefusesANullArgument()
    {
        using var workspace = new TempWorkspace();
        var backend = new LinuxLocalPtyBackend();

        var exception = await Should.ThrowAsync<ArgumentException>(() => backend.StartAsync(
            new TerminalSessionOptions(Shell, ["-c", null!], workspace.Path, 80, 24, new RecordingSink()),
            Token)).ConfigureAwait(true);

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task DoesNotPassHostVariablesOutsideTheAllowlistToTheCommand()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();

        Environment.SetEnvironmentVariable(SentinelName, "leaked");
        try
        {
            var session = await new LinuxLocalPtyBackend().StartAsync(
                new TerminalSessionOptions(
                    Shell,
                    ["-c", "read line; printf 'SEEN<%s>\\n' \"${OMPOS_TERMINAL_TEST_SENTINEL:-absent}\"; exit 0"],
                    workspace.Path,
                    80,
                    24,
                    sink),
                Token).ConfigureAwait(true);
            try
            {
                await session.WriteInputAsync("\n", Token).ConfigureAwait(true);
                var exitCode = await session.WaitForExitAsync(Token).ConfigureAwait(true);

                exitCode.ShouldBe(0);
            }
            finally
            {
                await session.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SentinelName, null);
        }

        sink.Text.ShouldContain("SEEN<absent>");
        sink.Text.ShouldNotContain("SEEN<leaked>");
    }

    [Fact]
    public async Task DisposeEndsARunningProcessAndReturns()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();

        var session = await new LinuxLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(Shell, ["-c", "echo 'PID<'$$'>'; exec sleep 600"], workspace.Path, 80, 24, sink),
            Token).ConfigureAwait(true);
        try
        {
            var pid = await WaitForPidAsync(session, sink, Token).ConfigureAwait(true);
            await session.DisposeAsync().ConfigureAwait(true);

            await AssertProcessEndedAsync(pid, Token).ConfigureAwait(true);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task OperationsAfterDisposeThrowObjectDisposedException()
    {
        using var workspace = new TempWorkspace();
        var session = await new LinuxLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(Shell, ["-c", "read line; exit 0"], workspace.Path, 80, 24, new RecordingSink()),
            Token).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);

        await Should.ThrowAsync<ObjectDisposedException>(() => session.WriteInputAsync("late\n", Token)).ConfigureAwait(true);
        Should.Throw<ObjectDisposedException>(() => session.Resize(100, 40));
        await Should.ThrowAsync<ObjectDisposedException>(() => session.WaitForExitAsync(Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task ConcurrentWritesAreAllDeliveredWithoutError()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();

        var session = await new LinuxLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(
                Shell,
                ["-c", "n=0; while [ $n -lt 20 ]; do read line; printf 'GOT<%s>\\n' \"$line\"; n=$((n+1)); done; exit 0"],
                workspace.Path,
                80,
                24,
                sink),
            Token).ConfigureAwait(true);
        try
        {
            var writes = Enumerable.Range(0, 20)
                .Select(index => session.WriteInputAsync($"line{index:D2}\n", Token))
                .ToArray();
            await Task.WhenAll(writes).ConfigureAwait(true);

            var exitCode = await session.WaitForExitAsync(Token).ConfigureAwait(true);

            exitCode.ShouldBe(0);
            for (var index = 0; index < 20; index++)
            {
                sink.Text.ShouldContain($"GOT<line{index:D2}>");
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Reads the process id the command printed as <c>PID&lt;n&gt;</c>, waiting until it appears in the output. If the session
    /// ends before the id appears, the test fails at once instead of waiting for a line that will never come.
    /// </summary>
    private static async Task<int> WaitForPidAsync(ITerminalSession session, RecordingSink sink, CancellationToken cancellationToken)
    {
        var exited = session.WaitForExitAsync(cancellationToken);
        while (true)
        {
            var text = sink.Text;
            var start = text.IndexOf("PID<", StringComparison.Ordinal);
            if (start >= 0)
            {
                var end = text.IndexOf('>', start);
                if (end > start)
                {
                    return int.Parse(text.AsSpan(start + 4, end - start - 4), CultureInfo.InvariantCulture);
                }
            }

            if (exited.IsCompleted)
            {
                Assert.Fail($"The command exited before it printed its process id. Output: {text}");
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Waits until the process has ended: its <c>/proc</c> entry is gone, or it is a zombie or dead.
    /// </summary>
    private static async Task AssertProcessEndedAsync(int pid, CancellationToken cancellationToken)
    {
        var stat = $"/proc/{pid}/stat";
        for (var attempt = 0; attempt < 250; attempt++)
        {
            string text;
            try
            {
                text = await File.ReadAllTextAsync(stat, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return;
            }

            // The state is the field after the parenthesised command name, which may itself contain spaces.
            var state = text[text.LastIndexOf(')') + 2];
            if (state is 'Z' or 'X')
            {
                return;
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(true);
        }

        Assert.Fail($"Process {pid} is still running after the session was disposed.");
    }

    /// <summary>
    /// Returns the physical path of a directory, with every symbolic link in it resolved. This is the path <c>pwd -P</c> reports.
    /// </summary>
    private static string PhysicalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var physical = Path.GetPathRoot(full)!;
        foreach (var name in Path.GetRelativePath(physical, full).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var component = Path.Combine(physical, name);
            var target = new DirectoryInfo(component).ResolveLinkTarget(returnFinalTarget: true);
            physical = target is null ? component : PhysicalPath(target.FullName);
        }

        return physical;
    }
}
