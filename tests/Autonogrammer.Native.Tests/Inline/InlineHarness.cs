using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.InlineEditing;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Tests.InlineEditing;

/// <summary>Shared helpers for the inline edit tests: builds the selection, shows the view and finds, clicks and presses its controls.</summary>
internal static class InlineHarness
{
    /// <summary>The buffer text. The selection is the word <c>brown</c>.</summary>
    public const string Original = "The quick brown fox.\n";

    /// <summary>The first character offset of the selection.</summary>
    public const int SelectionStart = 10;

    /// <summary>The length of the selection, in characters.</summary>
    public const int SelectionLength = 5;

    /// <summary>The instruction the edit sends with the selection.</summary>
    public const string Instruction = "Use a colour word";

    /// <summary>The text after the proposal <c>red</c> is accepted.</summary>
    public const string RedAccepted = "The quick red fox.\n";

    /// <summary>Creates a buffer holding the original text.</summary>
    /// <returns>The buffer.</returns>
    public static TextDocument NewDocument() => new(Original);

    /// <summary>Captures the selection of <c>brown</c> in the buffer with the fixture instruction.</summary>
    /// <param name="document">The buffer.</param>
    /// <returns>The inline edit with no proposal.</returns>
    public static InlineEdit NewEdit(TextDocument document) =>
        new(document, SelectionStart, SelectionLength, Instruction);

    /// <summary>Shows an inline edit view bound to the edit in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="edit">The edit to bind.</param>
    /// <returns>The shown window.</returns>
    public static InlineWindow Show(InlineEdit edit)
    {
        var window = new InlineWindow
        {
            Width = 900,
            Height = 900,
            Content = new InlineEditView { DataContext = edit },
        };
        window.Show();
        window.Activate();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Gets the view the window shows.</summary>
    public static InlineEditView View(Window window) => (InlineEditView)window.Content!;

    /// <summary>Gets the control with the given accessible name.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="window">The window that shows the view.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The control.</returns>
    public static T Named<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => string.Equals(AutomationProperties.GetName(control), name, StringComparison.Ordinal));

    /// <summary>Gets the proposed replacement input.</summary>
    public static TextBox Proposal(Window window) => View(window).FindControl<TextBox>("ProposalInput")!;

    /// <summary>Gets the change panel that shows the proposal as a reviewable change.</summary>
    public static StackPanel ChangePanel(Window window) => View(window).FindControl<StackPanel>("ChangePanel")!;

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

    /// <summary>Types a proposed replacement into the input.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="text">The proposal text.</param>
    public static void Type(Window window, string text)
    {
        Proposal(window).Text = text;
        ShellHarness.Settle(window);
    }
}
