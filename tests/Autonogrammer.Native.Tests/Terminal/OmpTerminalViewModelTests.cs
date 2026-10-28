using System.ComponentModel;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Terminal;
using Autonogrammer.Native.Views.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// The omp terminal model: it starts omp through its starter, shows starting, running, exited with a code and unavailable
/// with fix text, restarts, closes, and disposes the session it started so no omp child remains. The starter is a fake, so
/// no process starts.
/// </summary>
public sealed class OmpTerminalViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ANewModelIsIdleAndNothingStarted()
    {
        var starter = new FakeOmpTerminalStarter();

        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Idle);
        model.Session.ShouldBeNull();
        model.CanRestart.ShouldBeFalse();
        starter.Sizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartRunsOmpAtTheRememberedSizeAndExposesTheSession()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        model.SetSize(132, 43);

        await model.StartAsync().ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Running);
        model.StatusText.ShouldBe("omp is running");
        model.Session.ShouldBeSameAs(starter.Sessions.ShouldHaveSingleItem());
        starter.Sizes.ShouldBe([(132, 43)]);
    }

    [Fact]
    public async Task ASizeBelowOneIsIgnored()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        model.SetSize(100, 30);
        model.SetSize(0, 30);
        model.SetSize(100, -1);

        await model.StartAsync().ConfigureAwait(true);

        starter.Sizes.ShouldBe([(100, 30)]);
    }

    [Fact]
    public async Task OutputFromTheStarterSinkReachesTheOutputEvent()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        var received = new List<string>();
        model.OutputReceived += (_, e) => received.Add(e.Text);

        await model.StartAsync().ConfigureAwait(true);
        var sink = starter.Output.ShouldNotBeNull();
        sink.Write("hello");
        sink.Write(" world");

        received.ShouldBe(["hello", " world"]);
    }

    [Fact]
    public async Task WhenOmpExitsTheStatusSaysSoWithTheCodeAndTheSessionIsDisposed()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);
        var session = starter.Sessions.Single();

        session.Exit(3);
        await WaitForStateAsync(model, OmpTerminalState.Exited).ConfigureAwait(true);

        model.ExitCode.ShouldBe(3);
        model.StatusText.ShouldBe("omp exited with code 3");
        model.CanRestart.ShouldBeTrue();
        model.Session.ShouldBeNull();
        session.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task RestartAfterAnExitStartsOmpAgainAtTheLatestSizeAndAsksTheViewToClearTheScreen()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        var resets = 0;
        model.ScreenResetRequested += (_, _) => resets++;
        await model.StartAsync().ConfigureAwait(true);
        starter.Sessions[0].Exit(0);
        await WaitForStateAsync(model, OmpTerminalState.Exited).ConfigureAwait(true);
        model.SetSize(90, 25);

        model.RestartCommand.CanExecute(null).ShouldBeTrue();
        model.RestartCommand.Execute(null);
        await WaitForStateAsync(model, OmpTerminalState.Running).ConfigureAwait(true);

        starter.Sessions.Count.ShouldBe(2);
        starter.Sizes[1].ShouldBe((90, 25));
        model.Session.ShouldBeSameAs(starter.Sessions[1]);
        model.ExitCode.ShouldBeNull();
        resets.ShouldBe(1);
    }

    [Fact]
    public async Task RestartCannotRunWhileOmpIsRunning()
    {
        var model = new OmpTerminalViewModel(new FakeOmpTerminalStarter());
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);

        model.RestartCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task AMissingOmpIsUnavailableWithTheInstallGuidanceAndTheCheckedPaths()
    {
        var starter = new FakeOmpTerminalStarter();
        starter.Enqueue(new OmpTerminalNotFound(["/usr/bin/omp", "/opt/omp/bin/omp"], version: null, "Install omp, then choose Restart omp."));
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);

        await model.StartAsync().ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Unavailable);
        model.StatusText.ShouldBe("omp was not found");
        model.FixText.ShouldContain("Install omp, then choose Restart omp.", Case.Sensitive);
        model.FixText.ShouldContain("/usr/bin/omp", Case.Sensitive);
        model.FixText.ShouldContain("/opt/omp/bin/omp", Case.Sensitive);
        model.CanRestart.ShouldBeTrue();
        model.Session.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedLaunchIsUnavailableWithTheReasonAndAFix()
    {
        var starter = new FakeOmpTerminalStarter();
        starter.Enqueue(new OmpTerminalLaunchFailed("The project directory does not exist: /nowhere"));
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);

        await model.StartAsync().ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Unavailable);
        model.StatusText.ShouldBe("omp could not be started");
        model.FixText.ShouldContain("The project directory does not exist: /nowhere", Case.Sensitive);
        model.FixText.ShouldContain("Restart omp", Case.Sensitive);
    }

    [Fact]
    public async Task RestartAfterAMissingOmpRecoversWhenOmpIsThereNow()
    {
        var starter = new FakeOmpTerminalStarter();
        starter.Enqueue(new OmpTerminalNotFound([], version: null, "Install omp."));
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);

        model.RestartCommand.Execute(null);
        await WaitForStateAsync(model, OmpTerminalState.Running).ConfigureAwait(true);

        model.FixText.ShouldBeEmpty();
        model.StatusText.ShouldBe("omp is running");
    }

    [Fact]
    public async Task AStarterThatRejectsItsConfigurationIsUnavailableNotAnException()
    {
        var model = new OmpTerminalViewModel(new ThrowingStarter("The configured omp path must be absolute."));
        await using var modelScope = model.ConfigureAwait(true);

        await model.StartAsync().ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Unavailable);
        model.FixText.ShouldContain("The configured omp path must be absolute.", Case.Sensitive);
    }

    [Fact]
    public async Task StopDisposesTheSessionAndReturnsToIdle()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);
        var session = starter.Sessions.Single();

        await model.StopAsync().ConfigureAwait(true);

        session.Disposals.ShouldBe(1);
        model.State.ShouldBe(OmpTerminalState.Idle);
        model.Session.ShouldBeNull();
        model.StatusText.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoppingWhileOmpIsStillStartingDisposesTheSessionTheStartProduced()
    {
        var gate = new TaskCompletionSource();
        var starter = new FakeOmpTerminalStarter { Gate = gate.Task };
        var stuckSession = new FakeTerminalSession();
        await using var stuckScope = stuckSession.ConfigureAwait(true);
        var stuckStart = new OmpTerminalStarted("/usr/bin/omp", null, stuckSession);
        await using var stuckStartScope = stuckStart.ConfigureAwait(true);
        starter.Enqueue(stuckStart);
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);

        var start = model.StartAsync();
        model.State.ShouldBe(OmpTerminalState.Starting);
        await model.StopAsync().ConfigureAwait(true);
        gate.SetResult();
        await start.ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Idle);
        model.Session.ShouldBeNull();
        stuckSession.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task StoppingWhileOmpIsStartingCancelsALaunchThatHonoursCancellation()
    {
        var starter = new FakeOmpTerminalStarter { Gate = new TaskCompletionSource().Task, HonoursCancellation = true };
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);

        var start = model.StartAsync();
        await model.StopAsync().ConfigureAwait(true);
        await start.ConfigureAwait(true);

        model.State.ShouldBe(OmpTerminalState.Idle);
        starter.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartingTwiceWhileRunningStartsOnlyOneSession()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);

        await model.StartAsync().ConfigureAwait(true);

        starter.Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CloseStopsOmpAndRaisesCloseRequested()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);
        var closed = 0;
        model.CloseRequested += (_, _) => closed++;

        model.CloseCommand.Execute(null);
        await WaitForStateAsync(model, OmpTerminalState.Idle).ConfigureAwait(true);

        starter.Sessions.Single().Disposals.ShouldBe(1);
        closed.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeEndsTheSessionAndALaterStartDoesNothing()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);

        await model.DisposeAsync().ConfigureAwait(true);
        await model.StartAsync().ConfigureAwait(true);

        starter.Sessions.Single().Disposals.ShouldBe(1);
        starter.Sizes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SessionChangedReportsTheSessionWhenOmpStartsAndNullWhenItEnds()
    {
        var starter = new FakeOmpTerminalStarter();
        var model = new OmpTerminalViewModel(starter);
        await using var modelScope = model.ConfigureAwait(true);
        var changes = new List<object?>();
        model.SessionChanged += (_, e) => changes.Add(e.Session);

        await model.StartAsync().ConfigureAwait(true);
        starter.Sessions.Single().Exit(0);
        await WaitForStateAsync(model, OmpTerminalState.Exited).ConfigureAwait(true);

        changes.Count.ShouldBe(2);
        changes[0].ShouldBeSameAs(starter.Sessions.Single());
        changes[1].ShouldBeNull();
    }

    [Fact]
    public async Task StateChangesAreAnnouncedThroughPropertyChanged()
    {
        var model = new OmpTerminalViewModel(new FakeOmpTerminalStarter());
        await using var modelScope = model.ConfigureAwait(true);
        var names = new List<string?>();
        ((INotifyPropertyChanged)model).PropertyChanged += (_, e) => names.Add(e.PropertyName);

        await model.StartAsync().ConfigureAwait(true);

        names.ShouldContain(nameof(OmpTerminalViewModel.State), StringComparer.Ordinal);
        names.ShouldContain(nameof(OmpTerminalViewModel.StatusText), StringComparer.Ordinal);
        names.ShouldContain(nameof(OmpTerminalViewModel.CanRestart), StringComparer.Ordinal);
    }

    /// <summary>Waits for the model to reach a state. The model's own continuations run on the test's thread, so the test yields.</summary>
    private static async Task WaitForStateAsync(OmpTerminalViewModel model, OmpTerminalState state)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (model.State != state)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {state}; the model is {model.State}.");
            }

            await Task.Delay(10, Token).ConfigureAwait(true);
        }
    }

    private sealed class ThrowingStarter(string message) : IOmpTerminalStarter
    {
        public Task<OmpTerminalLaunchResult> StartAsync(
            int columns,
            int rows,
            IProcessOutputSink output,
            CancellationToken cancellationToken) => Task.FromException<OmpTerminalLaunchResult>(new ArgumentException(message, nameof(columns)));
    }
}
