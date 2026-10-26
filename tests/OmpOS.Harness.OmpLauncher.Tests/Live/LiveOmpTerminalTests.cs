using System.Globalization;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Tests.Live;
using OmpOS.Harness.Terminal;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpLauncher.Tests.Live;

/// <summary>
/// Live proof of the terminal half of Phase 48 criteria 1, 2 and 4 to 8: the real interactive <c>omp</c>, started by
/// <see cref="OmpTerminalLauncher"/> over <see cref="LinuxLocalPtyBackend"/> with the arguments and environment of
/// <see cref="OmpLaunchPlan.ForTerminal"/> in an isolated sandbox, loads the four plugins. Its plugin writes the connection
/// report and the steering heartbeat, so <see cref="OmpOS.Harness.AgentLoop.Connection.PluginConnectionStageSource"/> reads all
/// four plugins as connected, and two Ctrl+C within 150 ms close the session with no process of the run left. No message is ever
/// typed: the only input is the terminal's own answer to omp's device attributes query and the two Ctrl+C. Excluded from the
/// gate by its trait and skipped unless <c>OMPOS_LIVE_OMP=1</c>, so a plain test run never spawns omp; run it with
/// <c>OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.OmpLauncher.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveOmpTerminalTests
{
    private const string OptInVariable = "OMPOS_LIVE_OMP";
    private const string SessionId = "live-terminal";
    private const string CtrlC = "\u0003";
    private const int Columns = 120;
    private const int Rows = 40;

    /// <summary>Measured spawn to the first report is 17 to 97 s depending on host load; 180 s still fails a hang.</summary>
    private static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(180);

    /// <summary>PROBE-3: two presses 150 ms apart exit omp with code 0; 50 ms and 1 s apart do not.</summary>
    private static readonly TimeSpan CtrlCGap = TimeSpan.FromMilliseconds(150);

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Starts the real interactive omp with the planned terminal launch, proves all four plugins are connected and the
    /// heartbeat is fresh, closes the session with two Ctrl+C, and proves the process tree is gone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RealInteractiveOmpConnectsAllFourPluginsAndClosesOnTwoCtrlCWithoutAMessage()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the live terminal proof against the installed omp.");
        }

        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The live terminal proof uses the Linux pseudo-terminal backend and finds the omp process tree through /proc.");
        }

        var locatorOptions = OmpLocatorOptions.FromEnvironment(configuredPath: null);
        if (OmpLocator.Locate(locatorOptions) is OmpNotFound notFound)
        {
            Assert.Skip("omp is not installed; the live terminal proof is skipped. " + notFound.InstallGuidance);
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        using var sandbox = new LiveSandbox();
        var plan = LivePluginEvidence.PlanLaunch(sandbox, SessionId);
        sandbox.CreatePlannedDirectories(plan.Environment);

        var command = plan.ForTerminal();
        AssertTerminalCommand(plan, command);

        var marker = "PI_CODING_AGENT_DIR=" + command.Environment["PI_CODING_AGENT_DIR"];
        var sink = new LiveTerminalSink();
        await using var sinkScope = sink.ConfigureAwait(true);
        var started = await LaunchAsync(command, locatorOptions, sink, cancellationToken).ConfigureAwait(true);
        int? exitCode;
        await using (started.ConfigureAwait(true))
        {
            sink.Attach(started.Session);
            var exit = started.Session.WaitForExitAsync(cancellationToken);
            try
            {
                var observation = await LivePluginEvidence.ObserveAsync(plan, sandbox, SessionId, ReportTimeout, cancellationToken).ConfigureAwait(true);
                var running = LiveProcessScan.FindByEnvironment(marker);
                Report(started, sink, running, observation);
                AssertRunning(running, observation);
                LivePluginEvidence.AssertConnected(observation, expectedMode: "tui");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TestContext.Current.TestOutputHelper?.WriteLine("terminal output tail:" + Environment.NewLine + sink.ReadableTail());
                throw;
            }
            finally
            {
                exitCode = await CloseWithTwoCtrlCAsync(started.Session, exit).ConfigureAwait(true);
            }
        }

        var remaining = await LiveProcessScan.WaitForNoneAsync(marker, ExitTimeout, cancellationToken).ConfigureAwait(true);
        remaining.Select(process => process.Pid).ShouldBeEmpty("omp processes of the run survived the close.");
        AssertClosedOnCtrlC(exitCode);
    }

    private static async Task<OmpTerminalStarted> LaunchAsync(
        OmpTerminalCommand command,
        OmpLocatorOptions locatorOptions,
        LiveTerminalSink sink,
        CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string>(command.Environment, StringComparer.Ordinal)
        {
            ["ULTRATHINK_UPLIFT"] = "0",
            ["TERM"] = "xterm-256color",
            ["LANG"] = "C.UTF-8",
        };

        var launcher = new OmpTerminalLauncher(new LinuxLocalPtyBackend(), locatorOptions);
        var launch = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(command.WorkingDirectory, command.Arguments, Columns, Rows, sink) { Environment = environment },
            cancellationToken).ConfigureAwait(true);
        if (launch is OmpTerminalLaunchFailed failed)
        {
            Assert.Fail("The interactive omp could not be started: " + failed.Reason);
        }

        return launch.ShouldBeOfType<OmpTerminalStarted>();
    }

    private static void AssertClosedOnCtrlC(int? exitCode) =>
        exitCode.ShouldBe(
            0,
            string.Create(
                CultureInfo.InvariantCulture,
                $"omp did not exit with code 0 within {ExitTimeout.TotalSeconds:F0} s of two Ctrl+C {CtrlCGap.TotalMilliseconds:F0} ms apart (the disposal killed it)."));

    private static void AssertTerminalCommand(OmpLaunchPlan plan, OmpTerminalCommand command)
    {
        command.Arguments.Count.ShouldBe(plan.ExtraArguments.Count, "the terminal launch must add no resume reference and no first prompt: no message is ever typed.");
        command.Arguments[0].ShouldBe("--no-extensions", "the planned arguments must start with --no-extensions.");
        command.Arguments[1].ShouldBe("-e", "--no-extensions must be followed by -e.");
        command.Arguments.Contains("acp", StringComparer.Ordinal).ShouldBeFalse("the terminal launch must start the interactive omp, not acp.");
        command.Arguments.Contains("--plugin-dir", StringComparer.Ordinal).ShouldBeFalse("the plan must not use --plugin-dir.");
    }

    private static void AssertRunning(IReadOnlyList<LiveProcessInfo> running, LivePluginObservation observation)
    {
        running.ShouldNotBeEmpty("no process carries the run's isolated agent directory, so the run cannot be observed.");
        running.ShouldContain(
            process => process.HasArgumentRun("--no-extensions", "-e"),
            "no omp process was started with '--no-extensions -e ...'.");
        running.ShouldNotContain(
            process => process.Arguments.Contains("acp", StringComparer.Ordinal),
            "a process of the run was started as acp, so this is not the interactive omp.");
        running.ShouldContain(process => process.HasEnvironmentEntry("ULTRATHINK_UPLIFT=0"), "the child did not get ULTRATHINK_UPLIFT=0.");
        running.ShouldContain(
            process => process.Pid == observation.Report.Pid,
            "the process that wrote the connection report is not among the run's omp processes.");
    }

    private static async Task<int?> CloseWithTwoCtrlCAsync(ITerminalSession session, Task<int> exit)
    {
        try
        {
            await session.WriteInputAsync(CtrlC, CancellationToken.None).ConfigureAwait(true);
            await Task.Delay(CtrlCGap, CancellationToken.None).ConfigureAwait(true);
            await session.WriteInputAsync(CtrlC, CancellationToken.None).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // omp has already exited and no longer reads its input: the wait below returns its exit code.
        }

        try
        {
            return await exit.WaitAsync(ExitTimeout, CancellationToken.None).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static void Report(OmpTerminalStarted started, LiveTerminalSink sink, IReadOnlyList<LiveProcessInfo> running, LivePluginObservation observation)
    {
        var output = TestContext.Current.TestOutputHelper;
        if (output is null)
        {
            return;
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"omp: {started.ExecutablePath} version {started.Version ?? "unknown"}; device attributes queries answered: {sink.DeviceAttributesQueries}"));
        output.WriteLine("omp processes of the run: " + string.Join(", ", running.Select(process => process.Pid.ToString(CultureInfo.InvariantCulture))));
        output.WriteLine(observation.Describe());
    }
}
