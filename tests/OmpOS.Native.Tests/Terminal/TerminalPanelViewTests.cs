using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Views.Terminal;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Terminal;

/// <summary>
/// The terminal panel (ADE-14, ADR-0006) on the headless platform. Proposals wait for a decision, a declined proposal spawns
/// nothing, an approved command shows its streamed output, the terminal resizes with its window, no key reaches a process,
/// keyboard focus reaches the output, and every control has an accessible name.
/// </summary>
/// <remarks>
/// The PTY tests run <c>/bin/sh</c>, which every Linux test host has. They use real processes and wait for them with a deadline.
/// </remarks>
public sealed class TerminalPanelViewTests : IDisposable
{
    private const string Shell = "/bin/sh";
    private const string Completed = "Completed with exit code 0.";

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ompos-native-terminal-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates the workspace the panel's gate is rooted in.</summary>
    public TerminalPanelViewTests() => Directory.CreateDirectory(_workspace);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    /// <summary>With default settings a proposal waits for Approve or Decline, and nothing is spawned while it waits.</summary>
    [Fact]
    public async Task DefaultSettingsWaitForADecisionAndSpawnNothingOnTheirOwn()
    {
        await using var session = TestAppBuilder.StartSession();
        var runner = new CountingRunner();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace, null, runner);
            var window = Host(view);

            Propose(view, window, "tool", "--check");
            await WaitForAsync(window, () => TextNamed(view, "PendingText").Text!.StartsWith("Waiting for approval", StringComparison.Ordinal), "a pending proposal").ConfigureAwait(true);

            runner.Spawns.ShouldBe(0);
            ButtonNamed(view, "ApproveButton").IsEnabled.ShouldBeTrue();
            ButtonNamed(view, "DeclineButton").IsEnabled.ShouldBeTrue();
            ButtonNamed(view, "ProposeButton").IsEnabled.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Declining a proposal ends it without a spawn, and the panel is ready for a new proposal.</summary>
    [Fact]
    public async Task ADeclinedProposalSpawnsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        var runner = new CountingRunner();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace, null, runner);
            var window = Host(view);

            Propose(view, window, "tool", string.Empty);
            await WaitForAsync(window, () => ButtonNamed(view, "DeclineButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "DeclineButton"));
            await WaitForAsync(window, () => StatusIs(view, "Declined. Nothing ran."), "the declined status").ConfigureAwait(true);

            runner.Spawns.ShouldBe(0);
            ButtonNamed(view, "ApproveButton").IsEnabled.ShouldBeFalse();
            ButtonNamed(view, "ProposeButton").IsEnabled.ShouldBeTrue();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Approving a proposal runs it once through the runner and records the completed result.</summary>
    [Fact]
    public async Task ApprovingAProposalRunsItOnce()
    {
        await using var session = TestAppBuilder.StartSession();
        var runner = new CountingRunner();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace, null, runner);
            var window = Host(view);

            Propose(view, window, "tool", string.Empty);
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => StatusIs(view, Completed), "the completed status").ConfigureAwait(true);

            runner.Spawns.ShouldBe(1);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A proposal with a working directory outside the workspace is refused, and nothing is pending.</summary>
    [Fact]
    public async Task AProposalOutsideTheWorkspaceIsRefusedAndNotPending()
    {
        await using var session = TestAppBuilder.StartSession();
        var runner = new CountingRunner();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace, null, runner);
            var window = Host(view);

            Propose(view, window, "tool", string.Empty, "../outside");
            await WaitForAsync(window, () => TextNamed(view, "StatusText").Text!.Contains("outside the workspace root", StringComparison.Ordinal), "the refusal").ConfigureAwait(true);

            TextNamed(view, "PendingText").Text.ShouldBe("No command is waiting for approval.");
            runner.Spawns.ShouldBe(0);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Approve runs a real command in the pseudo-terminal, and its output appears in the terminal control.</summary>
    [Fact]
    public async Task ApprovedOutputAppearsInTheTerminalControl()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);

            Propose(view, window, Shell, "-c\nprintf hello");
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => StatusIs(view, Completed), "the completed status").ConfigureAwait(true);

            VisibleText(view).ShouldContain("hello");
            VisibleText(view).ShouldContain("[exit 0]");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Stop ends a running command: the process is killed and the panel returns to its idle state.</summary>
    [Fact]
    public async Task StopKillsARunningCommand()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);

            Propose(view, window, Shell, "-c\nprintf started; exec sleep 30");
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => VisibleText(view).Contains("started", StringComparison.Ordinal), "the started output").ConfigureAwait(true);
            ButtonNamed(view, "StopButton").IsEnabled.ShouldBeTrue();

            Click(ButtonNamed(view, "StopButton"));
            await WaitForAsync(window, () => StatusIs(view, "Stopped. The process was killed."), "the stopped status").ConfigureAwait(true);

            ButtonNamed(view, "StopButton").IsEnabled.ShouldBeFalse();
            ButtonNamed(view, "ProposeButton").IsEnabled.ShouldBeTrue();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>ANSI colour: the emulator gives the red text a non-default foreground on its cells, and the reset text keeps the default.</summary>
    [Fact]
    public async Task AnsiColourReachesTheTerminalCells()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);

            Propose(view, window, Shell, "-c\nprintf '\\033[31mred\\033[0m plain'");
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => StatusIs(view, Completed), "the completed status").ConfigureAwait(true);

            var terminal = Output(view).Terminal!;
            var row = LineStartingWith(terminal, "red");
            var defaultMode = XTerm.Buffer.AttributeData.Default.GetFgColorMode();
            var defaultColour = XTerm.Buffer.AttributeData.Default.GetFgColor();
            var red = row[0].Attributes;
            var plain = row[4].Attributes;
            (red.GetFgColorMode(), red.GetFgColor()).ShouldNotBe(
                (defaultMode, defaultColour),
                $"the red cell must carry a non-default foreground; red mode {red.GetFgColorMode()} colour {red.GetFgColor()}, default mode {defaultMode} colour {defaultColour}");
            (plain.GetFgColorMode(), plain.GetFgColor()).ShouldBe((defaultMode, defaultColour), "text after the reset must keep the default foreground");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// A window resize changes the size of the grid. A run reports the grid size once the layout has settled: the approval
    /// itself changes the panel's text, so the size can change during a run, and the command reads it after a short wait.
    /// </summary>
    [Fact]
    public async Task TheRunReportsTheGridSizeOnceTheLayoutSettles()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);
            var terminal = Output(view).Terminal!;
            var before = terminal.Cols;

            window.Width = 1100;
            window.Height = 760;
            await WaitForAsync(window, () => terminal.Cols > before, "the terminal to widen").ConfigureAwait(true);
            await WaitForStableGridAsync(window, terminal).ConfigureAwait(true);

            Propose(view, window, Shell, "-c\nsleep 1; stty size");
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => StatusIs(view, Completed), "the completed status").ConfigureAwait(true);

            VisibleText(view).ShouldContain($"{terminal.Rows} {terminal.Cols}");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Resizing the window while a command runs resizes the pseudo-terminal the command is attached to.</summary>
    [Fact]
    public async Task ResizingTheWindowDuringARunResizesThePseudoTerminal()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);
            var terminal = Output(view).Terminal!;

            var target = Path.Combine(_workspace, "grid-size.txt");

            // The command polls, for at most about 20 seconds, until the pseudo-terminal reports the grid size that the test
            // writes after the layout settles. No fixed delay decides the outcome.
            const string script = "printf ready; i=0; while [ ! -s \"$1\" ] && [ $i -lt 200 ]; do sleep 0.1; i=$((i+1)); done; "
                + "i=0; while [ \"$(stty size)\" != \"$(cat \"$1\")\" ] && [ $i -lt 200 ]; do sleep 0.1; i=$((i+1)); done; stty size";
            Propose(view, window, Shell, $"-c\n{script}\nsh\n{target}");
            await WaitForAsync(window, () => ButtonNamed(view, "ApproveButton").IsEnabled, "the decision buttons").ConfigureAwait(true);
            Click(ButtonNamed(view, "ApproveButton"));
            await WaitForAsync(window, () => VisibleText(view).Contains("ready", StringComparison.Ordinal), "the ready output").ConfigureAwait(true);

            window.Width = 1100;
            window.Height = 760;
            await WaitForStableGridAsync(window, terminal).ConfigureAwait(true);
            await File.WriteAllTextAsync(target, $"{terminal.Rows} {terminal.Cols}", Token).ConfigureAwait(true);

            await WaitForAsync(window, () => StatusIs(view, Completed), "the completed status").ConfigureAwait(true);
            VisibleText(view).ShouldContain($"{terminal.Rows} {terminal.Cols}");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// Showing the panel starts no process. The output control reports no live process, and on Linux the test host gains no child
    /// process while the panel is shown.
    /// </summary>
    [Fact]
    public async Task ShowingThePanelStartsNoProcess()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var childrenBefore = ChildProcessIds();
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);
            ShellHarness.Settle(window);

            Output(view).IsLive.ShouldBeFalse("the output control must not run a process when shown");
            ChildProcessIds().ShouldBe(childrenBefore, "showing the panel must not start a child process of the test host");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The child process identifiers of this process, read from <c>/proc</c>. Empty on platforms without <c>/proc</c>.</summary>
    private static List<string> ChildProcessIds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return [];
        }

        var children = new List<string>();
        foreach (var task in Directory.EnumerateDirectories("/proc/self/task"))
        {
            var file = Path.Combine(task, "children");
            if (File.Exists(file))
            {
                children.AddRange(File.ReadAllText(file).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        children.Sort(StringComparer.Ordinal);
        return children;
    }

    /// <summary>A key typed in the output control reaches no process: nothing is attached and the screen does not change.</summary>
    [Fact]
    public async Task TypingInTheOutputControlReachesNoProcess()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);
            var output = Output(view);
            var before = VisibleText(view);

            output.Focus();
            window.KeyPress(Key.X, default, PhysicalKey.X, "x");
            window.KeyPress(Key.Enter, default, PhysicalKey.Enter, null);
            ShellHarness.Settle(window);

            output.IsLive.ShouldBeFalse();
            VisibleText(view).ShouldBe(before);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Keyboard focus reaches the output control by Tab from the command entry.</summary>
    [Fact]
    public async Task TabReachesTheTerminalOutput()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            var window = Host(view);
            var output = Output(view);

            CommandInput(view).Focus();
            for (var presses = 0; presses < 12 && !HasKeyboardFocusWithin(window, output); presses++)
            {
                window.KeyPress(Key.Tab, default, PhysicalKey.Tab, null);
                ShellHarness.Settle(window);
            }

            HasKeyboardFocusWithin(window, output).ShouldBeTrue("Tab from the command entry must reach the terminal output");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>True when keyboard focus is on the control or one of its descendants.</summary>
    private static bool HasKeyboardFocusWithin(Window window, Visual control)
    {
        for (var current = window.FocusManager?.GetFocusedElement() as Visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, control))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every control has an accessible name in the Avalonia automation tree.</summary>
    [Fact]
    public async Task EveryControlHasAnAccessibleName()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TerminalPanelView(_workspace);
            Host(view);

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CommandInput"] = "Command",
                ["ArgumentsInput"] = "Arguments, one per line",
                ["DirectoryInput"] = "Working directory",
                ["ProposeButton"] = "Propose command",
                ["ApproveButton"] = "Approve command",
                ["DeclineButton"] = "Decline command",
                ["StopButton"] = "Stop running command",
                ["PendingText"] = "Pending command",
                ["StatusText"] = "Terminal status",
                ["Output"] = "Terminal output",
            };

            foreach (var (name, accessibleName) in expected)
            {
                AutomationProperties.GetName(view.FindControl<Control>(name)!).ShouldBe(accessibleName, name);
            }
        }, Token).ConfigureAwait(true);
    }

    private static Window Host(TerminalPanelView view)
    {
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static void Propose(TerminalPanelView view, Window window, string command, string arguments, string directory = "")
    {
        CommandInput(view).Text = command;
        ArgumentsInput(view).Text = arguments;
        DirectoryInput(view).Text = directory;
        Click(ButtonNamed(view, "ProposeButton"));
        ShellHarness.Settle(window);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Button ButtonNamed(TerminalPanelView view, string name) => view.FindControl<Button>(name)!;

    private static TextBlock TextNamed(TerminalPanelView view, string name) => view.FindControl<TextBlock>(name)!;

    private static bool StatusIs(TerminalPanelView view, string status) =>
        string.Equals(TextNamed(view, "StatusText").Text, status, StringComparison.Ordinal);

    private static TextBox CommandInput(TerminalPanelView view) => view.FindControl<TextBox>("CommandInput")!;

    private static TextBox ArgumentsInput(TerminalPanelView view) => view.FindControl<TextBox>("ArgumentsInput")!;

    private static TextBox DirectoryInput(TerminalPanelView view) => view.FindControl<TextBox>("DirectoryInput")!;

    private static TerminalControl Output(TerminalPanelView view) => view.FindControl<TerminalControl>("Output")!;

    private static string VisibleText(TerminalPanelView view) =>
        string.Join('\n', Output(view).Terminal!.GetVisibleLines()).TrimEnd();

    private static XTerm.Buffer.BufferLine LineStartingWith(XTerm.Terminal terminal, string prefix)
    {
        for (var index = 0; index < terminal.Buffer.Lines.Length; index++)
        {
            var line = terminal.Buffer.Lines[index];
            if (line is not null && line.TranslateToString(true, 0, terminal.Cols).StartsWith(prefix, StringComparison.Ordinal))
            {
                return line;
            }
        }

        throw new InvalidOperationException($"No terminal line starts with '{prefix}'.");
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
                throw new TimeoutException($"Timed out waiting for {because}; {PanelState(window)}.");
            }

            ShellHarness.Settle(window);
            await Task.Delay(25, Token).ConfigureAwait(true);
        }

        ShellHarness.Settle(window);
    }

    private static string PanelState(Window window) =>
        window.Content is TerminalPanelView view
            ? $"status '{TextNamed(view, "StatusText").Text}', pending '{TextNamed(view, "PendingText").Text}'"
            : "no panel";

    /// <summary>Counts spawns and returns an empty success, so a test can see whether the panel ran anything.</summary>
    private sealed class CountingRunner : IProcessRunner
    {
        /// <summary>The number of times the gate asked for a spawn.</summary>
        public int Spawns { get; private set; }

        /// <inheritdoc />
        public Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Spawns++;
            return Task.FromResult(new ProcessOutcome(0, string.Empty, string.Empty));
        }
    }
}
