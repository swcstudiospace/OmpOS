using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Native.Agents;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The shutdown of real child processes. The omp executable is a POSIX shell script written by the test, so no real omp, model or
/// session is started: it records its process identifier, answers the ACP handshake, opens a session, then holds every prompt open and
/// ignores the end of its input, so only the head's shutdown can end it.
/// </summary>
public sealed class EngineProcessTests
{
    private const UnixFileMode OwnerExecutable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const string FakeOmp =
        """
        #!/bin/sh
        echo $$ > "__PIDDIR__/$$.pid"
        while IFS= read -r line; do
          id=${line#*\"id\":}
          id=${id%%,*}
          case "$line" in
            *'"method":"initialize"'*) printf '{"jsonrpc":"2.0","id":%s,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true}}}\n' "$id" ;;
            *'"method":"session/new"'*) printf '{"jsonrpc":"2.0","id":%s,"result":{"sessionId":"native-proc"}}\n' "$id" ;;
          esac
        done
        sleep 600
        """;

    /// <summary>Closing a session stops the omp child it started and leaves the others running; disposing the head stops the rest: nothing is left running.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ClosingASessionAndDisposingTheHeadLeaveNoOmpChildRunning()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var pidDirectory = Directory.CreateDirectory(Path.Combine(tree.Root, "pids")).FullName;
        var head = AgentHead.Create(Options(tree, omp: null, ompPath: WriteFakeOmp(tree, pidDirectory)));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var closed = await StartTurnAsync(head, pidDirectory, [], ct).ConfigureAwait(true);
        var kept = await StartTurnAsync(head, pidDirectory, [closed.ProcessId], ct).ConfigureAwait(true);
        IsProcessRunning(closed.ProcessId).ShouldBeTrue();
        IsProcessRunning(kept.ProcessId).ShouldBeTrue();

        await head.Host.CloseSessionAsync(closed.Id, ct).ConfigureAwait(true);

        await EventuallyAsync(() => !IsProcessRunning(closed.ProcessId), "the closed session's omp to exit", ct).ConfigureAwait(true);
        IsProcessRunning(kept.ProcessId).ShouldBeTrue("closing one session leaves the others running");

        await head.DisposeAsync().ConfigureAwait(true);

        await EventuallyAsync(() => !IsProcessRunning(kept.ProcessId), "the remaining omp to exit", ct).ConfigureAwait(true);
        await closed.Turn.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);
        await kept.Turn.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);
    }

    private static string WriteFakeOmp(TempTree tree, string pidDirectory)
    {
        var path = Path.Combine(tree.Root, "omp");
        File.WriteAllText(path, FakeOmp.Replace("__PIDDIR__", pidDirectory, StringComparison.Ordinal).ReplaceLineEndings("\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerExecutable);
        }

        return path;
    }

    /// <summary>Creates a session, sends a prompt the fake never answers, waits until its engine opened a session, and reads the process identifier of the child it started.</summary>
    private static async Task<RunningTurn> StartTurnAsync(AgentHead head, string pidDirectory, int[] knownProcesses, CancellationToken cancellationToken)
    {
        var (id, slot) = await NewSlotAsync(head, cancellationToken).ConfigureAwait(true);
        var engine = await slot.EngineAsync(cancellationToken).ConfigureAwait(true);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Host.NativeSessionIdChanged += (_, _) => opened.TrySetResult();
        var turn = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (var unused in slot.Prompts.SubmitAsync("work", CancellationToken.None).ConfigureAwait(false))
                    {
                        // The fake never answers: the turn ends when the engine is stopped.
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // The engine was stopped under the turn, which ends it with a disconnect.
                }
            },
            CancellationToken.None);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(true);

        var processId = Directory.EnumerateFiles(pidDirectory, "*.pid")
            .Select(static file => int.Parse(Path.GetFileNameWithoutExtension(file), CultureInfo.InvariantCulture))
            .Single(process => !knownProcesses.Contains(process));
        return new RunningTurn(id, processId, turn);
    }

    private sealed record RunningTurn(string Id, int ProcessId, Task Turn);
}
