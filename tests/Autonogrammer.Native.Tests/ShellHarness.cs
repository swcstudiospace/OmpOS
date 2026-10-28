using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// Shared helpers for the headless tests: shows the shell, finds its named controls, drives keys and lays it out. A shell is the main
/// window over a <see cref="ShellFixture"/>: an Agent Panel whose head creates one runtime per session over a scripted send path.
/// </summary>
internal static class ShellHarness
{
    /// <summary>The identifier of the one session <see cref="Show(MotionPreference)"/> creates and selects.</summary>
    public const string FirstSessionId = "session-1";

    private static readonly ConditionalWeakTable<MainWindow, ShellFixture> Fixtures = new();

    /// <summary>
    /// Shows a new main window over a panel with one selected session whose send path is unconnected: every send is refused, as in a build
    /// with no engine. The window is 1280 x 800 and has had the layout passes a real window gets.
    /// </summary>
    /// <param name="motion">The motion setting to build the window with.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(MotionPreference motion = MotionPreference.System) => Show(script: null, motion);

    /// <summary>Shows the main window over a new fixture with one selected session over <paramref name="script"/>, and remembers the fixture, so <see cref="FixtureOf"/> finds it.</summary>
    /// <param name="script">The session's script, or null for an unconnected send path.</param>
    /// <param name="motion">The motion setting to build the window with.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="workspaceRoot">The workspace folder of the window's tools, or null for the current folder.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(SessionScript? script, MotionPreference motion = MotionPreference.System, double width = 1280, double height = 800, string? workspaceRoot = null)
    {
        var fixture = NewFixture(script);
        var window = fixture.Show(motion, width, height, workspaceRoot);
        Fixtures.AddOrUpdate(window, fixture);
        return window;
    }

    private static ShellFixture NewFixture(SessionScript? script) => ShellFixture.CreateWithSession(script);

    /// <summary>Shows the main window over a fixture and remembers the fixture, so <see cref="FixtureOf"/> finds it.</summary>
    /// <param name="fixture">The fixture to show.</param>
    /// <param name="motion">The motion setting to build the window with.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="workspaceRoot">The workspace folder of the window's tools, or null for the current folder.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(ShellFixture fixture, MotionPreference motion = MotionPreference.System, double width = 1280, double height = 800, string? workspaceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var window = fixture.Show(motion, width, height, workspaceRoot);
        Fixtures.AddOrUpdate(window, fixture);
        return window;
    }

    /// <summary>Gets the fixture a window was shown over.</summary>
    /// <param name="window">A window shown by this harness.</param>
    /// <returns>The fixture.</returns>
    public static ShellFixture FixtureOf(MainWindow window) =>
        Fixtures.TryGetValue(window, out var fixture) ? fixture : throw new InvalidOperationException("The window was not shown by ShellHarness.");

    /// <summary>Gets the runtime of the session the window shows.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The selected session's runtime.</returns>
    public static AgentSessionRuntime RuntimeOf(MainWindow window)
    {
        var fixture = FixtureOf(window);
        return fixture.Runtime(fixture.Panel.Selected?.Id ?? throw new InvalidOperationException("No session is selected."));
    }

    /// <summary>Gets the transcript view model of the session the window shows.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The transcript view model.</returns>
    public static SessionViewModel SessionOf(MainWindow window) => RuntimeOf(window).Session;

    /// <summary>Gets the composer view model of the session the window shows.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The composer view model.</returns>
    public static ComposerViewModel ComposerModelOf(MainWindow window) => RuntimeOf(window).Composer;

    /// <summary>Gets the main view the window shows. The shell's named controls live in that view, not in the window.</summary>
    /// <param name="window">The shown window.</param>
    /// <returns>The main view.</returns>
    public static MainView MainViewOf(MainWindow window) => (MainView)window.Content!;

    /// <summary>Runs pending dispatcher work and one layout pass, so item containers and state copy exist.</summary>
    /// <param name="window">The window to settle.</param>
    public static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var layoutManager = window.GetLayoutManager()
            ?? throw new InvalidOperationException("The window has no layout manager; it must be shown before it is settled.");
        layoutManager.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Gets the agent list of the rail.</summary>
    public static ListBox Rows(MainWindow window) => ShellTree.Named<ListBox>(MainViewOf(window), "AgentRows")!;

    /// <summary>Gets the New agent button of the rail.</summary>
    public static Button NewAgent(MainWindow window) => ShellTree.Named<Button>(MainViewOf(window), "NewAgentButton")!;

    /// <summary>Gets the filter box of the rail.</summary>
    public static TextBox Filter(MainWindow window) => ShellTree.Named<TextBox>(MainViewOf(window), "AgentFilter")!;

    /// <summary>Gets the skip link.</summary>
    public static Button SkipLink(MainWindow window) => MainViewOf(window).FindControl<Button>("SkipLink")!;

    /// <summary>Gets the transcript view.</summary>
    public static SessionView Transcript(MainWindow window) => (SessionView)MainViewOf(window).FindControl<ContentControl>("TranscriptHost")!.Content!;

    /// <summary>Gets the transcript's scroll region, which is the keyboard stop for the transcript.</summary>
    public static ScrollViewer Scroll(MainWindow window) => Transcript(window).FindControl<ScrollViewer>("TranscriptScroll")!;

    /// <summary>Gets the composer view.</summary>
    public static ComposerView Composer(MainWindow window) => (ComposerView)MainViewOf(window).FindControl<ContentControl>("ComposerHost")!.Content!;

    /// <summary>Gets the prompt input.</summary>
    public static TextBox Prompt(MainWindow window) => Composer(window).FindControl<TextBox>("PromptInput")!;

    /// <summary>Gets the send control.</summary>
    public static Button Send(MainWindow window) => Composer(window).FindControl<Button>("SendButton")!;

    /// <summary>Gets the stop control.</summary>
    public static Button Stop(MainWindow window) => Composer(window).FindControl<Button>("StopButton")!;

    /// <summary>Gets the status line.</summary>
    public static TextBlock Status(MainWindow window) => MainViewOf(window).FindControl<TextBlock>("StatusLine")!;

    /// <summary>Gets the transcript's state heading.</summary>
    public static TextBlock TranscriptHeading(MainWindow window) => Transcript(window).FindControl<TextBlock>("StateHeading")!;

    /// <summary>Gets the transcript's state body.</summary>
    public static TextBlock TranscriptBody(MainWindow window) => Transcript(window).FindControl<TextBlock>("StateBody")!;

    /// <summary>Shows the Tools view of the rail, where the tool toggles are, so the toggles are tab stops and can be clicked.</summary>
    /// <param name="window">The shown window.</param>
    public static void ShowToolsView(MainWindow window)
    {
        MainViewOf(window).FindControl<ToggleButton>("ToolsViewToggle")!.IsChecked = true;
        Settle(window);
    }

    /// <summary>Shows the Sessions view of the rail.</summary>
    /// <param name="window">The shown window.</param>
    public static void ShowSessionsView(MainWindow window)
    {
        MainViewOf(window).FindControl<ToggleButton>("SessionsViewToggle")!.IsChecked = true;
        Settle(window);
    }

    /// <summary>Presses and releases one key on the window, as the keyboard would.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="shift">Whether Shift is held.</param>
    /// <param name="control">Whether Control is held.</param>
    public static void Press(MainWindow window, Key key, PhysicalKey physical, bool shift = false, bool control = false)
    {
        var keys = (shift ? KeyModifiers.Shift : KeyModifiers.None) | (control ? KeyModifiers.Control : KeyModifiers.None);
        window.KeyPress(key, (RawInputModifiers)keys, physical, null);
        Settle(window);
    }

    /// <summary>Clicks a button as the pointer would: raises its click event, then settles the window.</summary>
    /// <param name="window">The shown window that owns the button.</param>
    /// <param name="button">The button to click.</param>
    public static void Click(MainWindow window, Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        RaiseClick(button);
        Settle(window);
    }

    /// <summary>Raises the click of a button and runs its command afterwards, as a button does when a pointer press or a key activates it.</summary>
    /// <param name="button">The button to click.</param>
    public static void RaiseClick(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        var click = new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent);
        button.RaiseEvent(click);
        if (!click.Handled && button.IsEffectivelyEnabled && button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
    }

    /// <summary>Waits for the work a key press or click started in the composer to finish.</summary>
    /// <param name="window">The window that owns the composer.</param>
    public static async Task DrainAsync(MainWindow window)
    {
        var composer = Composer(window);
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSettled(object? sender, EventArgs e) => settled.TrySetResult();

        composer.WorkSettled += OnSettled;
        try
        {
            if (composer.IsWorking)
            {
                await settled.Task.ConfigureAwait(true);
            }
        }
        finally
        {
            composer.WorkSettled -= OnSettled;
        }
        Settle(window);
    }

    /// <summary>Walks up from the test output to the directory that holds <c>Autonogrammer.slnx</c>.</summary>
    /// <returns>The repository root.</returns>
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Autonogrammer.slnx must be reachable by walking up from the test output.");
    }
}
