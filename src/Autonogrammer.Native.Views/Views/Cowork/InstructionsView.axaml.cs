using Autonogrammer.Presentation.Cowork.Instructions;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>Load, edit and save for account-global instructions, including the 8 KiB message.</summary>
public partial class InstructionsView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    private GlobalInstructionsModel? _model;

    /// <summary>Creates the view.</summary>
    public InstructionsView()
    {
        InitializeComponent();
        LimitText.Text = GlobalInstructionsModel.LimitMessage;
        LoadButton.Click += OnLoad;
        SaveButton.Click += OnSave;
        Editor.TextChanged += OnEdited;
    }

    /// <summary>Gets the named controls in keyboard order.</summary>
    public IReadOnlyList<Control> KeyboardOrder => [Editor, LoadButton, SaveButton];

    /// <summary>Applies reduced motion.</summary>
    /// <param name="reduced">True when motion is reduced.</param>
    public void ApplyMotion(bool reduced) => Classes.Set(ReducedMotionClass, reduced);

    /// <summary>Binds the model.</summary>
    /// <param name="model">The instructions model.</param>
    public void Attach(GlobalInstructionsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        Editor.Text = model.Draft;
        StatusText.Text = model.Sentence ?? model.State.ToString();
        Editor.IsReadOnly = model.IsReadOnly;
    }

    /// <summary>Refreshes status after a model change.</summary>
    public void Refresh()
    {
        if (_model is null)
        {
            return;
        }

        StatusText.Text = _model.Sentence ?? _model.State.ToString();
        Editor.IsReadOnly = _model.IsReadOnly;
    }

    private void OnEdited(object? sender, TextChangedEventArgs e) => _model?.Edit(Editor.Text ?? string.Empty);

    private void OnLoad(object? sender, RoutedEventArgs e) => _ = LoadAsync();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    private async Task LoadAsync()
    {
        if (_model is null)
        {
            return;
        }

        await _model.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        Editor.Text = _model.Draft;
        Refresh();
    }

    private async Task SaveAsync()
    {
        if (_model is null)
        {
            return;
        }

        await _model.SaveAsync(CancellationToken.None).ConfigureAwait(true);
        Refresh();
    }
}
