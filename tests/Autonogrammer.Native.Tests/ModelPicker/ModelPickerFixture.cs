using Avalonia.Controls;
using Avalonia.Layout;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Composer;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>
/// A real composer view over a real composer model and a real picker binding, shown at the bottom of a window so its flyout opens upward
/// into free space. Only the model control, the engine side of the picker, is scripted.
/// </summary>
internal sealed class ModelPickerFixture : IDisposable
{
    /// <summary>The models the fixture's control reports.</summary>
    internal static readonly OmpModelChoice[] Models =
    [
        new("anthropic/claude-sonnet", "Claude Sonnet"),
        new("anthropic/claude-opus", "Claude Opus"),
        new("openai/gpt-x", "GPT X"),
    ];

    private readonly ModelPickerBinding? _binding;

    private ModelPickerFixture(FakeModelControl control, ModelPickerBinding? binding, ComposerViewModel composer, ComposerView view, Window window)
    {
        Control = control;
        _binding = binding;
        Composer = composer;
        View = view;
        Window = window;
    }

    /// <summary>Gets the scripted model control.</summary>
    internal FakeModelControl Control { get; }

    /// <summary>Gets the composer model.</summary>
    internal ComposerViewModel Composer { get; }

    /// <summary>Gets the composer view.</summary>
    internal ComposerView View { get; }

    /// <summary>Gets the model chip of the view.</summary>
    internal ModelPickerView Chip => View.ModelChip;

    /// <summary>Gets the window that shows the view.</summary>
    internal Window Window { get; }

    /// <summary>Shows the composer. Call it on the dispatcher thread of a headless session.</summary>
    /// <param name="withPicker">Whether the composer carries a model picker. Without one the chip must stay hidden.</param>
    /// <param name="answer">The answer to a switch request, or null to accept it.</param>
    /// <returns>The fixture. The caller disposes it.</returns>
    internal static ModelPickerFixture Open(bool withPicker = true, Func<string, OmpModelSwitchResult>? answer = null)
    {
        var control = new FakeModelControl { Answer = answer };
        control.Report(Models, Models[0].Value);
        var binding = withPicker ? new ModelPickerBinding(control, connect: null) : null;
        var composer = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask) { ModelPicker = binding?.Picker };
        var view = new ComposerView { DataContext = composer, VerticalAlignment = VerticalAlignment.Bottom };
        var window = ViewHarness.Show(view, width: 900, height: 700);
        return new ModelPickerFixture(control, binding, composer, view, window);
    }

    /// <summary>Lets the dispatcher and layout settle after a change.</summary>
    internal void Settle() => ViewHarness.Settle(Window);

    /// <summary>Closes the window and stops following the control.</summary>
    public void Dispose()
    {
        _binding?.Dispose();
        Window.Close();
    }
}
