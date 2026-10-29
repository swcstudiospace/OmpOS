using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Avalonia.Controls;

namespace Autonogrammer.Native.Settings;

/// <summary>The real Settings window (<see cref="SettingsWindow"/>) behind the host's <see cref="ISettingsWindowHandle"/>.</summary>
internal sealed class AvaloniaSettingsWindow : ISettingsWindowHandle
{
    private readonly SettingsWindow _window;
    private readonly SettingsWindowModel _model;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaSettingsWindow"/> class over a model.</summary>
    /// <param name="model">The window model. The window draws it and closes when it reports closed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public AvaloniaSettingsWindow(SettingsWindowModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _window = new SettingsWindow(model);
        _window.Closed += OnWindowClosed;
    }

    /// <inheritdoc/>
    public event EventHandler? Closed;

    /// <inheritdoc/>
    public void Show(Window? owner)
    {
        if (owner is null)
        {
            _window.Show();
            return;
        }

        _window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _window.Show(owner);
    }

    /// <inheritdoc/>
    public void Activate() => _window.Activate();

    /// <inheritdoc/>
    public void CloseDiscardingChanges()
    {
        foreach (var page in _model.Pages)
        {
            page.Discard();
        }

        _ = _model.RequestClose();
    }

    private void OnWindowClosed(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);
}
