using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Iciclecreek.Terminal;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Views.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// The interactive omp terminal on the headless platform. The omp it starts is <c>/bin/sh -c script</c> over a real
/// pseudo-terminal: its output is drawn in the emulator, typed input reaches it, the pseudo-terminal follows the grid, an exit
/// is announced with its code and offers Restart, an unavailable omp shows its fix text, and leaving the tree or closing leaves
/// no process behind. The tests use real processes and wait for them with a deadline.
/// </summary>
public sealed class OmpTerminalViewTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "autonogrammer-omp-terminal-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates the project directory the fake omp starts in.</summary>
    public OmpTerminalViewTests() => Directory.CreateDirectory(_directory);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>The output of the fake omp is drawn, typed input reaches it, and its exit is announced with the code.</summary>
    [Fact]
    public async Task OutputIsDrawnTypedInputReachesOmpAndTheExitIsAnnouncedWithItsCode()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'MARKER-READY\\n'; read line; printf 'GOT<%s>\\n' \"$line\"; exit 3", _directory);
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);

            await WaitForAsync(window, () => VisibleText(view).Contains("MARKER-READY", StringComparison.Ordinal), "the marker").ConfigureAwait(true);
            model.State.ShouldBe(OmpTerminalState.Running);
            TextNamed(view, "StatusText").Text.ShouldBe("omp is running");
            ButtonNamed(view, "RestartButton").IsVisible.ShouldBeFalse();

            await Output(view).SendInputAsync("typed\r", Token).ConfigureAwait(true);
            await WaitForAsync(window, () => model.State == OmpTerminalState.Exited, "the exit").ConfigureAwait(true);

            VisibleText(view).ShouldContain("GOT<typed>", Case.Sensitive);
            model.ExitCode.ShouldBe(3);
            TextNamed(view, "StatusText").Text.ShouldBe("omp exited with code 3");
            ButtonNamed(view, "RestartButton").IsVisible.ShouldBeTrue();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Restart omp starts the fake omp again on a cleared screen.</summary>
    [Fact]
    public async Task RestartStartsOmpAgainOnAClearedScreen()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'RUN-%s-END\\n' \"$$\"; exit 0", _directory);
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);

            await WaitForAsync(window, () => RunIds(VisibleText(view)).Count == 1 && model.State == OmpTerminalState.Exited, "the first run").ConfigureAwait(true);
            var firstRun = RunIds(VisibleText(view)).Single();

            Click(ButtonNamed(view, "RestartButton"));
            await WaitForAsync(
                window,
                () => starter.Sizes.Count == 2 && model.State == OmpTerminalState.Exited && RunIds(VisibleText(view)).Any(id => !string.Equals(id, firstRun, StringComparison.Ordinal)),
                "the second run").ConfigureAwait(true);

            RunIds(VisibleText(view)).ShouldNotContain(firstRun, StringComparer.Ordinal, "the first run's line was cleared");
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The pseudo-terminal of omp follows the size of the control's grid.</summary>
    [Fact]
    public async Task ThePseudoTerminalFollowsTheGridOfTheControl()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'SIZE-READY\\n'; read line; stty size; exit 0", _directory);
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);
            await WaitForAsync(window, () => VisibleText(view).Contains("SIZE-READY", StringComparison.Ordinal), "the marker").ConfigureAwait(true);

            window.Width = 1100;
            window.Height = 500;
            var terminal = Output(view).Terminal!;
            await WaitForStableGridAsync(window, terminal).ConfigureAwait(true);
            var expected = $"{terminal.Rows} {terminal.Cols}";
            await Output(view).SendInputAsync("go\r", Token).ConfigureAwait(true);
            await WaitForAsync(window, () => model.State == OmpTerminalState.Exited, "the exit").ConfigureAwait(true);

            VisibleText(view).ShouldContain(expected, Case.Sensitive);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A missing omp shows its fix text, selectable, in place of the terminal, with Restart.</summary>
    [Fact]
    public async Task AMissingOmpShowsSelectableFixTextAndRestart()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            starter.Enqueue(new OmpTerminalNotFound(["/usr/local/bin/omp"], version: null, "Install omp, then choose Restart omp."));
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);

            await WaitForAsync(window, () => model.State == OmpTerminalState.Unavailable, "the unavailable state").ConfigureAwait(true);

            var fix = view.FindControl<SelectableTextBlock>("FixText")!;
            fix.Text.ShouldNotBeNull().ShouldContain("Install omp, then choose Restart omp.", Case.Sensitive);
            fix.Text.ShouldContain("/usr/local/bin/omp", Case.Sensitive);
            view.FindControl<Control>("UnavailablePanel")!.IsVisible.ShouldBeTrue();
            Output(view).IsVisible.ShouldBeFalse();
            TextNamed(view, "StatusText").Text.ShouldBe("omp was not found");
            ButtonNamed(view, "RestartButton").IsVisible.ShouldBeTrue();

            Click(ButtonNamed(view, "RestartButton"));
            await WaitForAsync(window, () => model.State == OmpTerminalState.Running, "the running state").ConfigureAwait(true);

            view.FindControl<Control>("UnavailablePanel")!.IsVisible.ShouldBeFalse();
            Output(view).IsVisible.ShouldBeTrue();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Leaving the visual tree ends the omp process.</summary>
    [Fact]
    public async Task LeavingTheVisualTreeEndsTheOmpProcess()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'PID<%s>\\n' \"$$\"; exec sleep 600", _directory);
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);
            await WaitForAsync(window, () => ParsePid(VisibleText(view)) is not null, "the process id").ConfigureAwait(true);
            var pid = ParsePid(VisibleText(view)).GetValueOrDefault();

            window.Content = null;
            ShellHarness.Settle(window);

            await AssertProcessEndedAsync(pid, window).ConfigureAwait(true);
            await WaitForAsync(window, () => model.State == OmpTerminalState.Idle, "the idle state").ConfigureAwait(true);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Close stops the omp process and raises the close request.</summary>
    [Fact]
    public async Task CloseEndsTheOmpProcessAndRequestsTheClose()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'PID<%s>\\n' \"$$\"; exec sleep 600", _directory);
            var model = new OmpTerminalViewModel(starter);
            await using var modelScope = model.ConfigureAwait(true);
            var closeRequests = 0;
            model.CloseRequested += (_, _) => closeRequests++;
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);
            await WaitForAsync(window, () => ParsePid(VisibleText(view)) is not null, "the process id").ConfigureAwait(true);
            var pid = ParsePid(VisibleText(view)).GetValueOrDefault();

            Click(ButtonNamed(view, "CloseButton"));
            await WaitForAsync(window, () => closeRequests == 1, "the close request").ConfigureAwait(true);

            await AssertProcessEndedAsync(pid, window).ConfigureAwait(true);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Disposing the model with the view still shown ends the omp process.</summary>
    [Fact]
    public async Task DisposingTheModelEndsTheOmpProcess()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var starter = new ShellScriptOmpStarter("printf 'PID<%s>\\n' \"$$\"; exec sleep 600", _directory);
            var model = new OmpTerminalViewModel(starter);
            var view = new OmpTerminalView { DataContext = model };
            var window = Host(view);
            await WaitForAsync(window, () => ParsePid(VisibleText(view)) is not null, "the process id").ConfigureAwait(true);
            var pid = ParsePid(VisibleText(view)).GetValueOrDefault();

            await model.DisposeAsync().ConfigureAwait(true);

            await AssertProcessEndedAsync(pid, window).ConfigureAwait(true);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every control has an accessible name, the status is a polite live region, and sizes use the target token.</summary>
    [Fact]
    public async Task EveryControlHasAnAccessibleNameAndTheStatusIsAPoliteLiveRegion()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(() =>
        {
            var view = new OmpTerminalView();

            var expected = new (string Name, string AccessibleName)[]
            {
                ("Output", "omp terminal"),
                ("RestartButton", "Restart omp"),
                ("CloseButton", "Close omp terminal"),
                ("StatusText", "omp status"),
                ("FixText", "Why omp is unavailable"),
            };
            foreach (var (name, accessibleName) in expected)
            {
                AutomationProperties.GetName(view.FindControl<Control>(name)!).ShouldBe(accessibleName, name);
            }

            AutomationProperties.GetLiveSetting(view.FindControl<Control>("StatusText")!).ShouldBe(AutomationLiveSetting.Polite);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    private static Window Host(OmpTerminalView view)
    {
        var window = new Window { Width = 900, Height = 500, Content = view };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Button ButtonNamed(OmpTerminalView view, string name) => view.FindControl<Button>(name)!;

    private static TextBlock TextNamed(OmpTerminalView view, string name) => view.FindControl<TextBlock>(name)!;

    private static TerminalControl Output(OmpTerminalView view) => view.FindControl<TerminalControl>("Output")!;

    private static string VisibleText(OmpTerminalView view) =>
        Output(view).Terminal is { } terminal ? string.Join('\n', terminal.GetVisibleLines()).TrimEnd() : string.Empty;

    /// <summary>Returns the process id of each <c>RUN-id-END</c> line the screen shows.</summary>
    private static List<string> RunIds(string text)
    {
        var ids = new List<string>();
        var from = 0;
        while (true)
        {
            var start = text.IndexOf("RUN-", from, StringComparison.Ordinal);
            if (start < 0)
            {
                return ids;
            }

            var end = text.IndexOf("-END", start, StringComparison.Ordinal);
            if (end < 0)
            {
                return ids;
            }

            ids.Add(text[(start + 4)..end]);
            from = end + 4;
        }
    }

    private static int? ParsePid(string text)
    {
        var start = text.IndexOf("PID<", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var end = text.IndexOf('>', start);
        return end > start && int.TryParse(text.AsSpan(start + 4, end - start - 4), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            ? pid
            : null;
    }

    /// <summary>Waits until the process has ended: its <c>/proc</c> entry is gone, or it is a zombie or dead.</summary>
    private static async Task AssertProcessEndedAsync(int pid, Window window)
    {
        var stat = $"/proc/{pid}/stat";
        for (var attempt = 0; attempt < 500; attempt++)
        {
            string text;
            try
            {
                text = await File.ReadAllTextAsync(stat, Token).ConfigureAwait(true);
            }
            catch (IOException) when (!File.Exists(stat))
            {
                return;
            }

            var state = text[(text.LastIndexOf(')') + 2)..][0];
            if (state is 'Z' or 'X')
            {
                return;
            }

            ShellHarness.Settle(window);
            await Task.Delay(20, Token).ConfigureAwait(true);
        }

        Assert.Fail($"The process {pid} was still running.");
    }

    /// <summary>Waits, for at most 20 seconds, until the terminal keeps the same rows and columns for about 500 ms.</summary>
    private static async Task WaitForStableGridAsync(Window window, XTerm.Terminal terminal)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var last = (terminal.Rows, terminal.Cols);
        var unchanged = 0;
        while (unchanged < 10)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Timed out waiting for the terminal grid to settle.");
            }

            ShellHarness.Settle(window);
            await Task.Delay(50, Token).ConfigureAwait(true);

            var now = (terminal.Rows, terminal.Cols);
            if (now == last)
            {
                unchanged++;
            }
            else
            {
                unchanged = 0;
                last = now;
            }
        }
    }

    private static async Task WaitForAsync(Window window, Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {because}.");
            }

            ShellHarness.Settle(window);
            await Task.Delay(25, Token).ConfigureAwait(true);
        }

        ShellHarness.Settle(window);
    }
}
