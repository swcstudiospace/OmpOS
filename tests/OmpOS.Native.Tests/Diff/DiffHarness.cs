using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Views.Diff;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Tests.Diff;

/// <summary>Shared helpers for the diff review tests: shows the review view, finds its hunk controls, clicks and presses keys.</summary>
internal static class DiffHarness
{
    /// <summary>The original text the fixture diff was made against: two hunks, the first and second each change one place.</summary>
    public const string Original = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";

    /// <summary>A diff with two hunks: <c>beta</c> becomes <c>BETA</c>, and <c>iota</c> is inserted before <c>theta</c>.</summary>
    public const string TwoHunks =
        "--- a/notes.txt\n"
        + "+++ b/notes.txt\n"
        + "@@ -1,4 +1,4 @@\n"
        + " alpha\n"
        + "-beta\n"
        + "+BETA\n"
        + " gamma\n"
        + " delta\n"
        + "@@ -6,3 +6,4 @@\n"
        + " zeta\n"
        + " eta\n"
        + "+iota\n"
        + " theta\n";

    /// <summary>The text when only the first hunk is accepted.</summary>
    public const string FirstAccepted = "alpha\nBETA\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";

    /// <summary>The text when only the second hunk is accepted.</summary>
    public const string SecondAccepted = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\niota\ntheta\n";

    /// <summary>The text when both hunks are accepted.</summary>
    public const string BothAccepted = "alpha\nBETA\ngamma\ndelta\nepsilon\nzeta\neta\niota\ntheta\n";

    /// <summary>Creates the review of the fixture diff against the fixture original.</summary>
    /// <returns>A review with every hunk pending.</returns>
    public static DiffReview NewReview() => new(Original, UnifiedDiff.Parse(TwoHunks));

    /// <summary>Shows a diff review view bound to the review in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="review">The review to bind.</param>
    /// <returns>The shown window.</returns>
    public static DiffWindow Show(DiffReview review)
    {
        var window = new DiffWindow
        {
            Width = 900,
            Height = 900,
            Content = new DiffReviewView { DataContext = review },
        };
        window.Show();
        window.Activate();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Gets the view the window shows.</summary>
    public static DiffReviewView View(Window window) => (DiffReviewView)window.Content!;

    /// <summary>Gets the button with the given accessible name.</summary>
    /// <param name="window">The window that shows the view.</param>
    /// <param name="name">The accessible name, such as <c>Accept hunk 1</c>.</param>
    /// <returns>The button.</returns>
    public static Button Named(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    /// <summary>Gets the resulting text the view shows.</summary>
    public static string Result(Window window) => View(window).FindControl<TextBox>("ResultBox")!.Text ?? string.Empty;

    /// <summary>Clicks the centre of a control with the left button, as a pointer would, and settles the layout.</summary>
    /// <param name="window">The shown window that contains the control.</param>
    /// <param name="control">The control to click.</param>
    public static void Click(Window window, Control control)
    {
        var centre = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(centre, window) ?? throw new InvalidOperationException("The control is not in the window.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        ShellHarness.Settle(window);
    }

    /// <summary>Presses and releases one key on the window, as the keyboard would, and settles the layout.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    public static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, default, physical, null);
        ShellHarness.Settle(window);
    }
}
