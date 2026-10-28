using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.Hooks;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>
/// A shown hooks view in a headless window, with helpers that drive it the way a keyboard does and read what it draws. Every
/// action is started by a key press, and <see cref="WaitForIdleAsync"/> waits until the model has finished it.
/// </summary>
internal sealed class HooksScreen
{
    private HooksScreen(HooksModel model, HooksView view, Window window)
    {
        Model = model;
        View = view;
        Window = window;
    }

    /// <summary>Gets the model the view draws.</summary>
    public HooksModel Model { get; }

    /// <summary>Gets the view.</summary>
    public HooksView View { get; }

    /// <summary>Gets the window that shows the view.</summary>
    public Window Window { get; }

    /// <summary>Shows the view over a source and lets the first layout pass run. Loading starts when the view is shown.</summary>
    /// <param name="source">The hooks source.</param>
    /// <returns>The shown screen.</returns>
    public static HooksScreen Show(IHookRegistrySource source)
    {
        var model = new HooksModel(source);
        var view = new HooksView { DataContext = model };
        var window = new Window { Width = 900, Height = 1400, Content = view };
        window.Show();
        var screen = new HooksScreen(model, view, window);
        screen.Settle();
        return screen;
    }

    /// <summary>Runs pending dispatcher work and one layout pass.</summary>
    public void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Window.GetLayoutManager()?.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Waits until no load or action is running, then settles the window.</summary>
    /// <returns>A task that completes when the model is idle.</returns>
    public async Task WaitForIdleAsync()
    {
        if (Model.IsBusy)
        {
            var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged(object? sender, EventArgs e)
            {
                if (!Model.IsBusy)
                {
                    idle.TrySetResult();
                }
            }

            Model.Changed += OnChanged;
            try
            {
                await idle.Task.ConfigureAwait(true);
            }
            finally
            {
                Model.Changed -= OnChanged;
            }
        }

        Settle();
    }

    /// <summary>Moves focus to a control by its accessible name and presses a key on it, then waits for the action.</summary>
    /// <param name="accessibleName">The accessible name of the control.</param>
    /// <param name="key">The key to press: Enter or Space activates a button.</param>
    /// <returns>A task that completes when the resulting action is finished.</returns>
    public async Task ActivateAsync(string accessibleName, Key key)
    {
        FindButton(accessibleName).Focus();
        Window.KeyPress(key, default, PhysicalKeyOf(key), null);
        Window.KeyRelease(key, default, PhysicalKeyOf(key), null);
        await WaitForIdleAsync().ConfigureAwait(true);
    }

    /// <summary>Presses Tab once, then settles.</summary>
    public void PressTab()
    {
        Window.KeyPress(Key.Tab, default, PhysicalKey.Tab, null);
        Settle();
    }

    /// <summary>Gets the accessible name of the focused button, or null when focus is not on a button.</summary>
    /// <returns>The name, or null.</returns>
    public string? FocusedButtonName()
    {
        var focused = TopLevel.GetTopLevel(Window)?.FocusManager?.GetFocusedElement() as Button;
        return focused is null ? null : AutomationProperties.GetName(focused);
    }

    /// <summary>Focuses a button by its accessible name.</summary>
    /// <param name="accessibleName">The accessible name of the button.</param>
    public void Focus(string accessibleName) => FindButton(accessibleName).Focus();

    /// <summary>Finds a button in the view by its accessible name. It throws when the button is not drawn.</summary>
    /// <param name="accessibleName">The accessible name.</param>
    /// <returns>The button.</returns>
    public Button FindButton(string accessibleName) => View.GetVisualDescendants()
        .OfType<Button>()
        .Single(button => string.Equals(AutomationProperties.GetName(button), accessibleName, StringComparison.Ordinal));

    /// <summary>Gets the accessible names of every button in the view, in tab order of the tree.</summary>
    /// <returns>The names.</returns>
    public IReadOnlyList<string> ButtonNames() => View.GetVisualDescendants()
        .OfType<Button>()
        .Select(button => AutomationProperties.GetName(button) ?? string.Empty)
        .ToArray();

    /// <summary>Gets a named text block of the view.</summary>
    /// <param name="name">The control's x:Name.</param>
    /// <returns>The text block.</returns>
    public TextBlock Text(string name) => View.FindControl<TextBlock>(name)
        ?? throw new InvalidOperationException($"The view has no text block named '{name}'.");

    /// <summary>Returns whether a named control is drawn, including through its visible ancestors.</summary>
    /// <param name="name">The control's x:Name.</param>
    public bool IsShown(string name) => Named(name).IsEffectivelyVisible;

    /// <summary>Gets a named control of the view, of any type.</summary>
    /// <param name="name">The control's x:Name.</param>
    /// <returns>The control.</returns>
    public Control Named(string name) => View.FindControl<Control>(name)
        ?? throw new InvalidOperationException($"The view has no control named '{name}'.");

    /// <summary>Counts the degraded markers that are drawn.</summary>
    /// <returns>The number of visible markers.</returns>
    public int DrawnDegradedMarkers() => View.GetVisualDescendants()
        .OfType<TextBlock>()
        .Count(text => string.Equals(text.Text, "Degraded", StringComparison.Ordinal) && text.IsEffectivelyVisible);

    private static PhysicalKey PhysicalKeyOf(Key key) => key switch
    {
        Key.Enter => PhysicalKey.Enter,
        Key.Space => PhysicalKey.Space,
        Key.Tab => PhysicalKey.Tab,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "The hooks tests press Enter, Space or Tab only."),
    };
}
