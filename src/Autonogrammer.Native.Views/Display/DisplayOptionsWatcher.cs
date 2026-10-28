using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Display;

/// <summary>
/// Tracks the display switches that apply to one control: the inherited <see cref="DisplayOptions.SettingsProperty"/> and the property
/// changes of the model it holds. The watcher listens to the model only while the control is in the visual tree, so a detached control
/// never stays subscribed; it reads the property again when the control is attached, so a control created before the shell set the
/// settings still sees them. It calls back on attach and on every change.
/// </summary>
internal sealed class DisplayOptionsWatcher
{
    private readonly Control _control;
    private readonly Action<DisplaySettingsValues> _changed;
    private DisplaySettings? _settings;
    private bool _attached;

    /// <summary>Creates a watcher for a control.</summary>
    /// <param name="control">The control whose effective settings are tracked.</param>
    /// <param name="changed">Called with the effective values on attach and whenever they change.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> or <paramref name="changed"/> is null.</exception>
    public DisplayOptionsWatcher(Control control, Action<DisplaySettingsValues> changed)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(changed);

        _control = control;
        _changed = changed;
        _control.AttachedToVisualTree += OnAttached;
        _control.DetachedFromVisualTree += OnDetached;
        _control.PropertyChanged += OnControlPropertyChanged;
        if (TopLevel.GetTopLevel(_control) is not null)
        {
            _attached = true;
            Track(DisplayOptions.GetSettings(_control));
        }
    }

    /// <summary>Gets the effective values now: those of the settings the control inherits, or the defaults when it has none.</summary>
    public DisplaySettingsValues Values => _settings?.Values ?? DisplaySettingsValues.Defaults;

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _attached = true;
        Track(DisplayOptions.GetSettings(_control));
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
            _settings = null;
        }
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_attached && e.Property == DisplayOptions.SettingsProperty)
        {
            Track(DisplayOptions.GetSettings(_control));
        }
    }

    private void Track(DisplaySettings? settings)
    {
        if (!ReferenceEquals(_settings, settings))
        {
            if (_settings is not null)
            {
                _settings.PropertyChanged -= OnSettingsChanged;
            }

            _settings = settings;
            if (_settings is not null)
            {
                _settings.PropertyChanged += OnSettingsChanged;
            }
        }

        _changed(Values);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => _changed(Values);
}
