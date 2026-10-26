using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Views;
using OmpOS.Presentation;

namespace OmpOS.Native.Tests;

/// <summary>
/// Shared helpers for the headless tests: shows the shell, finds its named controls, drives keys and lays it out.
/// </summary>
internal static class ShellHarness
{
    /// <summary>Shows a new main window and gives it the layout pass a real window gets.</summary>
    /// <param name="motion">The motion setting to build the window with.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(MotionPreference motion = MotionPreference.System)
    {
        var window = new MainWindow(motion);
        window.Show();
        Settle(window);
        return window;
    }

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

    /// <summary>Gets the session list view.</summary>
    public static ConversationListView List(MainWindow window) => MainViewOf(window).FindControl<ConversationListView>("ConversationList")!;

    /// <summary>Gets the row list inside the session list.</summary>
    public static ListBox Rows(MainWindow window) => List(window).FindControl<ListBox>("ConversationRows")!;

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

    /// <summary>Gets the session list's state heading.</summary>
    public static TextBlock ListHeading(MainWindow window) => List(window).FindControl<TextBlock>("StateHeading")!;

    /// <summary>Gets the session list's state body.</summary>
    public static TextBlock ListBody(MainWindow window) => List(window).FindControl<TextBlock>("StateBody")!;

    /// <summary>Gets the transcript's state heading.</summary>
    public static TextBlock TranscriptHeading(MainWindow window) => Transcript(window).FindControl<TextBlock>("StateHeading")!;

    /// <summary>Gets the transcript's state body.</summary>
    public static TextBlock TranscriptBody(MainWindow window) => Transcript(window).FindControl<TextBlock>("StateBody")!;

    /// <summary>Presses and releases one key on the window, as the keyboard would.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static void Press(MainWindow window, Key key, PhysicalKey physical, bool shift = false)
    {
        var modifiers = shift ? (RawInputModifiers)KeyModifiers.Shift : default;
        window.KeyPress(key, modifiers, physical, null);
        Settle(window);
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

    /// <summary>Walks up from the test output to the directory that holds <c>OmpOS.slnx</c>.</summary>
    /// <returns>The repository root.</returns>
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("OmpOS.slnx must be reachable by walking up from the test output.");
    }
}
