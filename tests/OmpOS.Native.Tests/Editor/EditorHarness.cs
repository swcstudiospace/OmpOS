using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using OmpOS.Native.Views.Editor;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Tests.Editor;

/// <summary>Shared helpers for the editor tests: shows the editor view in a window, finds its controls and sends keys.</summary>
internal static class EditorHarness
{
    /// <summary>The query the find tests search for. It matches the lowercase and the capitalised occurrences of the fixture.</summary>
    public const string Needle = "needle";

    /// <summary>Creates an editor state over a new tab set.</summary>
    /// <returns>The view model the tests drive.</returns>
    public static EditorViewModel NewModel() => new(new EditorTabs());

    /// <summary>Shows an editor view bound to the model in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="model">The editor state to bind.</param>
    /// <returns>The shown window.</returns>
    public static EditorWindow Show(EditorViewModel model)
    {
        var window = new EditorWindow
        {
            Width = 1000,
            Height = 700,
            Content = new EditorView { DataContext = model },
        };
        window.Show();
        window.Activate();
        Settle(window);
        return window;
    }

    /// <summary>
    /// Runs pending dispatcher work and several layout passes, so the editor's text view has laid out its visible lines and
    /// the work a change started has finished.
    /// </summary>
    /// <param name="window">The window to settle.</param>
    public static void Settle(Window window)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.GetLayoutManager()?.ExecuteLayoutPass();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Gets the editor view the window shows.</summary>
    public static EditorView View(Window window) => (EditorView)window.Content!;

    /// <summary>Gets the code editor.</summary>
    public static TextEditor Code(Window window) => View(window).FindControl<TextEditor>("CodeEditor")!;

    /// <summary>Gets the find text box.</summary>
    public static TextBox FindInput(Window window) => View(window).FindControl<TextBox>("FindInput")!;

    /// <summary>Gets the Next match button.</summary>
    public static Button NextButton(Window window) => View(window).FindControl<Button>("NextMatchButton")!;

    /// <summary>Gets the Previous match button.</summary>
    public static Button PreviousButton(Window window) => View(window).FindControl<Button>("PreviousMatchButton")!;

    /// <summary>Gets the match-case check box.</summary>
    public static CheckBox MatchCase(Window window) => View(window).FindControl<CheckBox>("MatchCaseCheck")!;

    /// <summary>Gets the find status line.</summary>
    public static TextBlock Status(Window window) => View(window).FindControl<TextBlock>("MatchStatusText")!;

    /// <summary>Gets the tab strip.</summary>
    public static ItemsControl TabStrip(Window window) => View(window).FindControl<ItemsControl>("TabStrip")!;

    /// <summary>Gets the toggle that selects an open tab, by its file name.</summary>
    public static ToggleButton TabToggle(Window window, string name) =>
        View(window).GetVisualDescendants().OfType<ToggleButton>().Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    /// <summary>Gets the close button of an open tab, by its file name.</summary>
    public static Button TabClose(Window window, string name) =>
        View(window).GetVisualDescendants().OfType<Button>().Single(button => string.Equals(AutomationProperties.GetName(button), $"Close {name}", StringComparison.Ordinal));

    /// <summary>Types a query into the find box, as the user would, and lets the find update settle.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="query">The text to set in the find box.</param>
    public static void Type(Window window, string query)
    {
        FindInput(window).Text = query;
        Settle(window);
    }

    /// <summary>Turns match case on or off through the check box, and lets the find update settle.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="value">Whether match case is on.</param>
    public static void SetMatchCase(Window window, bool value)
    {
        MatchCase(window).IsChecked = value;
        Settle(window);
    }

    /// <summary>Presses and releases one key on the window, as the keyboard would, and settles the layout.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static void Press(Window window, Key key, PhysicalKey physical, bool shift = false)
    {
        var modifiers = shift ? (RawInputModifiers)KeyModifiers.Shift : default;
        window.KeyPress(key, modifiers, physical, null);
        Settle(window);
    }

    /// <summary>Clicks the centre of a control with the left button, as a pointer would, and settles the layout.</summary>
    /// <param name="window">The shown window that contains the control.</param>
    /// <param name="control">The control to click.</param>
    public static void Click(Window window, Control control)
    {
        var centre = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(centre, window) ?? throw new InvalidOperationException("The control is not in the window.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle(window);
    }

    /// <summary>Gets the character offsets of every occurrence of a query, the offsets a find must select in order.</summary>
    /// <param name="text">The searched text.</param>
    /// <param name="query">The query.</param>
    /// <param name="comparison">How the query matches.</param>
    /// <returns>The offsets in document order.</returns>
    public static IReadOnlyList<int> OccurrenceOffsets(string text, string query, StringComparison comparison)
    {
        var offsets = new List<int>();
        for (var index = text.IndexOf(query, comparison); index >= 0; index = text.IndexOf(query, index + query.Length, comparison))
        {
            offsets.Add(index);
        }

        return offsets;
    }

    /// <summary>
    /// Builds the 10000-line fixture: every tenth line holds "needle", and every tenth line offset by five holds "Needle".
    /// The other lines hold "filler".
    /// </summary>
    /// <returns>The fixture text.</returns>
    public static string TenThousandLineFixture() =>
        string.Join('\n', Enumerable.Range(0, 10000).Select(line => (line % 10) switch
        {
            0 => $"row {line} needle",
            5 => $"row {line} Needle",
            _ => $"row {line} filler",
        }));
}
