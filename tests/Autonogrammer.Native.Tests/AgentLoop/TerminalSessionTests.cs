using System.Runtime.Versioning;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// Omp terminal sessions of the production head over a fake omp executable (a shell script that records itself and never contacts a
/// model): the registry record and the private directories every omp process needs, the exact command line and environment the planner
/// gives the terminal, the lifecycle the registry mirrors from the terminal, the reopen with and without a known omp session, and the
/// end of the omp child when the session is closed or the head is disposed.
/// </summary>
public sealed class TerminalSessionTests
{
    private const string Secret = "tok-7c1e-never-print-me";

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static readonly string[] AllowedHostVariables =
        ["HOME", "LANG", "LC_ALL", "OLDPWD", "OMP_PROFILE", "PATH", "PI_CODING_AGENT_DIR", "PWD", "SHLVL", "TERM", "_"];

    /// <summary>A terminal session is a Kind Terminal record titled "omp terminal N"; it and a chat session get private directories for the steering inbox, the connection report and the Swarm state.</summary>
    [Fact]
    public async Task CreatingASessionWritesTheRecordAndTheOwnerOnlyDirectories()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var first = await head.Host.CreateTerminalSessionAsync(ct).ConfigureAwait(true);
        var second = await head.Host.CreateTerminalSessionAsync(ct).ConfigureAwait(true);
        var chat = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);

        var registry = head.Host.Registry!;
        registry.TryGet(first, out var firstRecord).ShouldBeTrue();
        registry.TryGet(second, out var secondRecord).ShouldBeTrue();
        registry.TryGet(chat, out var chatRecord).ShouldBeTrue();
        firstRecord!.Kind.ShouldBe(SessionKind.Terminal);
        firstRecord.Title.ShouldBe("omp terminal 1");
        secondRecord!.Kind.ShouldBe(SessionKind.Terminal);
        secondRecord.Title.ShouldBe("omp terminal 2");
        chatRecord!.Kind.ShouldBe(SessionKind.Chat);
        head.Host.Catalog.Snapshot().Count(static summary => summary.Kind == SessionKind.Terminal).ShouldBe(2);
        foreach (var record in new[] { firstRecord, secondRecord, chatRecord })
        {
            foreach (var directory in SessionDirectories(record.StateDirectory))
            {
                Directory.Exists(directory).ShouldBeTrue(directory);
                if (!OperatingSystem.IsWindows())
                {
                    File.GetUnixFileMode(directory).ShouldBe(OwnerOnly, directory);
                }
            }
        }
    }

    /// <summary>The omp of a terminal session starts with the planner's exact argument array, the plan's environment names (the steering, connection and Swarm variables among them) and the workspace as its directory; no argument is acp and no value of the plan is printed.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task TheTerminalStartsOmpWithThePlannedArgumentsAndEnvironment()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var fake = FakeTerminalOmp.Waiting(tree);
        using var substrate = new StubSubstrateHandler();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, Substrate("http://127.0.0.1:7410"), substrate, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (id, terminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);

                await terminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => File.Exists(fake.PidFile), "the fake omp to record itself", ct).ConfigureAwait(true);

                var plan = await head.Host.SlotOf(id)!.PlanAsync(ct).ConfigureAwait(true);
                string[] expectedArguments =
                [
                    "--no-extensions",
                    "-e", tree.PluginPath("claude-ultrathink"),
                    "-e", tree.PluginPath("agent-swarm/omp"),
                    "-e", tree.PluginPath("agent-substrate/packages/projector/omp/substrate-status.ts"),
                    "-e", tree.PluginPath("agent-substrate/packages/projector/omp/substrate-skills.ts"),
                    "-e", tree.PluginPath("gsd-core-omp"),
                ];
                fake.Arguments().ShouldBe(expectedArguments);
                fake.Arguments().ShouldNotContain("acp", StringComparer.Ordinal);
                fake.Arguments().ShouldNotContain("--plugin-dir", StringComparer.Ordinal);
                plan.Launch.ExtraArguments.ShouldBe(expectedArguments);
                var names = fake.EnvironmentNames();
                names.Except(plan.Launch.Environment.Keys.Concat(AllowedHostVariables), StringComparer.Ordinal).ShouldBeEmpty("every variable omp received is planned or an allowed host variable");
                foreach (var name in plan.Launch.Environment.Keys)
                {
                    names.ShouldContain(name, StringComparer.Ordinal);
                }

                names.ShouldContain(OmpLaunchPlanner.SteerDirectoryVariable, StringComparer.Ordinal);
                names.ShouldContain(OmpLaunchPlanner.ConnectionFileVariable, StringComparer.Ordinal);
                names.ShouldContain("SWARM_GSD_STATE_FILE", StringComparer.Ordinal);
                fake.WorkingDirectory().ShouldBe(tree.Workspace);
                head.Host.Registry!.TryGet(id, out var running).ShouldBeTrue();
                running!.Lifecycle.ShouldBe(SessionLifecycle.Running);
                head.Host.SlotOf(id)!.TurnActive.ShouldBeTrue("a running terminal keeps its heartbeat");

                var command = plan.Launch.ForTerminal();
                command.ToString().ShouldNotContain(Secret, Case.Sensitive);
                plan.Launch.ToString().ShouldNotContain(Secret, Case.Sensitive);
                (await File.ReadAllTextAsync(fake.RecordFile, ct).ConfigureAwait(true)).ShouldNotContain(Secret, Case.Sensitive);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The registry follows the omp of a terminal to its end: an exit with code 0 is Completed, any other code is Failed, and the heartbeat of the running terminal stops.</summary>
    [Theory]
    [InlineData(0, SessionLifecycle.Completed)]
    [InlineData(3, SessionLifecycle.Failed)]
    [SupportedOSPlatform("linux")]
    public async Task TheExitCodeOfOmpDecidesTheLifecycle(int exitCode, SessionLifecycle expected)
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var fake = FakeTerminalOmp.Exiting(tree, exitCode);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (id, terminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);

                await terminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => terminal.State == OmpTerminalState.Exited, "the fake omp to exit", ct).ConfigureAwait(true);

                terminal.ExitCode.ShouldBe(exitCode);
                head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
                record!.Lifecycle.ShouldBe(expected);
                head.Host.SlotOf(id)!.TurnActive.ShouldBeFalse();
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An omp that is not installed is a Failed session with the terminal's own fix text; the head and the other sessions keep working.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AMissingOmpFailsTheSessionAndKeepsTheHeadWorking()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var missing = Path.Combine(tree.Root, "no-such-dir", "omp");
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, terminalLocator: new OmpLocatorOptions(missing, [], [])));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (id, terminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);

                await terminal.StartAsync().ConfigureAwait(true);

                terminal.State.ShouldBe(OmpTerminalState.Unavailable);
                terminal.FixText.ShouldNotBeNullOrWhiteSpace();
                head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
                record!.Lifecycle.ShouldBe(SessionLifecycle.Failed);
                (await head.Host.CreateSessionAsync(ct).ConfigureAwait(true)).ShouldNotBeNullOrWhiteSpace();
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Closing a terminal session ends the omp child it started and records the session Paused; disposing the head ends the omp of a session that is still open.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ClosingASessionAndDisposingTheHeadLeaveNoOmpChildRunning()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var fake = FakeTerminalOmp.Waiting(tree);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (closedId, closedTerminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);
                await closedTerminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => File.Exists(fake.PidFile), "the first fake omp to record itself", ct).ConfigureAwait(true);
                var closedProcess = fake.ProcessId();
                IsProcessRunning(closedProcess).ShouldBeTrue();

                await head.Panel.CloseSessionAsync(closedId, ct).ConfigureAwait(true);

                await RunControlsHarness.UntilAsync(() => !IsProcessRunning(closedProcess), "the closed session's omp to exit", ct).ConfigureAwait(true);
                head.Host.IsClosed(closedId).ShouldBeTrue();
                head.Host.Registry!.TryGet(closedId, out var paused).ShouldBeTrue();
                paused!.Lifecycle.ShouldBe(SessionLifecycle.Paused);

                fake.Reset();
                var (_, keptTerminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);
                await keptTerminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => File.Exists(fake.PidFile), "the second fake omp to record itself", ct).ConfigureAwait(true);
                var keptProcess = fake.ProcessId();
                IsProcessRunning(keptProcess).ShouldBeTrue();

                await head.DisposeAsync().ConfigureAwait(true);

                await RunControlsHarness.UntilAsync(() => !IsProcessRunning(keptProcess), "the remaining omp to exit", ct).ConfigureAwait(true);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>When the registry knows the omp session of a terminal, reopening it starts omp with <c>--resume</c> and that reference; the reference is not printed anywhere.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AKnownOmpSessionIsResumedWithTheResumeArgument()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var fake = FakeTerminalOmp.Waiting(tree);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (id, terminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);
                head.Host.Registry!.SetAcpSession(id, "acp-9", "native-9").ShouldBeTrue();

                await terminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => File.Exists(fake.PidFile), "the fake omp to record itself", ct).ConfigureAwait(true);

                var arguments = fake.Arguments();
                arguments.TakeLast(2).ShouldBe(["--resume", "native-9"]);
                head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
                record!.ToString().ShouldNotContain("native-9", Case.Sensitive);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A terminal that was lost and has no known omp session starts a new omp without <c>--resume</c> and says so in its stages instead of pretending to resume.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AReopenedTerminalWithoutAKnownOmpSessionStartsFreshAndSaysSo()
    {
        SkipUnlessLinux();
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var fake = FakeTerminalOmp.Waiting(tree);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var (id, terminal) = await OpenTerminalAsync(head, ct).ConfigureAwait(true);
                var slot = head.Host.SlotOf(id)!;
                head.Host.Registry!.SetLifecycle(id, SessionLifecycle.Lost).ShouldBeTrue();

                await head.Host.ReattachAsync(slot, ct).ConfigureAwait(true);

                // The session's runtime already reads the stage source, so the notice reaches the runtime's timeline, not a second reader.
                var timeline = head.Panel.Selected.ShouldNotBeNull().Timeline;
                await RunControlsHarness.UntilAsync(() => ResumeNotice(timeline) is not null, "the fresh-start notice in the timeline", ct).ConfigureAwait(true);
                await terminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => File.Exists(fake.PidFile), "the fake omp to record itself", ct).ConfigureAwait(true);

                var notice = ResumeNotice(timeline).ShouldNotBeNull();
                notice.Status.ShouldBe(StageStatus.Degraded);
                notice.Detail.ShouldNotBeNull().ShouldContain("starts a new omp session", Case.Sensitive);
                fake.Arguments().ShouldNotContain("--resume", StringComparer.Ordinal);
                head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
                record!.Lifecycle.ShouldBe(SessionLifecycle.Running);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    private static StageRow? ResumeNotice(StageTimelineModel timeline) =>
        timeline.Blocks.SelectMany(static block => block.Rows).SingleOrDefault(static row => string.Equals(row.StageId, "terminal-resume", StringComparison.Ordinal));

    private static Func<string, string?> Substrate(string address) => name => name switch
    {
        AgentLoopEnvironment.SubstrateUrlVariable => address,
        AgentLoopEnvironment.DefaultSubstrateTokenVariable => Secret,
        _ => null,
    };

    private static string[] SessionDirectories(string sessionDirectory)
    {
        var steer = SessionStateLayout.SteerDirectory(sessionDirectory);
        return
        [
            sessionDirectory,
            steer,
            Path.Combine(steer, "inbox"),
            Path.Combine(steer, "acks"),
            Path.Combine(steer, "done"),
            Path.GetDirectoryName(SessionStateLayout.ConnectionFile(sessionDirectory))!,
            SessionStateLayout.SwarmDirectory(sessionDirectory),
        ];
    }

    /// <summary>Creates a terminal session, selects it so the panel opens its runtime, and returns its identifier and terminal model.</summary>
    private static async Task<(string Id, OmpTerminalViewModel Terminal)> OpenTerminalAsync(AgentHead head, CancellationToken cancellationToken)
    {
        await head.Panel.NewTerminalAsync(cancellationToken).ConfigureAwait(true);
        var selected = head.Panel.Selected.ShouldNotBeNull();
        var runtime = AgentSessionRuntime.For(selected).ShouldNotBeNull();
        return (selected.Id, runtime.Terminal.ShouldNotBeNull());
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }
}
