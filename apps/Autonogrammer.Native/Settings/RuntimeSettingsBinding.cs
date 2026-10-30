using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// Connects the Runtime page to the head: it builds the page over the saved values and, after every successful save, writes the values
/// to the settings file and puts them in force. The page has no source of its own, so this is where its save persists. When the file
/// cannot be written the values still take effect for this run, and <see cref="Failed"/> says why they will not survive a restart.
/// </summary>
internal sealed class RuntimeSettingsBinding
{
    private readonly FileSettingsStore _store;
    private readonly Action<RuntimeSettingsValues> _apply;

    /// <summary>Initializes a new instance of the <see cref="RuntimeSettingsBinding"/> class.</summary>
    /// <param name="store">The settings file.</param>
    /// <param name="apply">Puts the values in force for this run. It runs after every successful save, whether or not the file could be written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="apply"/> is null.</exception>
    public RuntimeSettingsBinding(FileSettingsStore store, Action<RuntimeSettingsValues> apply)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(apply);
        _store = store;
        _apply = apply;
    }

    /// <summary>Occurs with the reason when a saved value could not be written to the settings file.</summary>
    public event EventHandler<RuntimeSettingsSaveFailedEventArgs>? Failed;

    /// <summary>Builds the Runtime page over the values the file holds, or the defaults when the file cannot be used.</summary>
    /// <returns>The page. The binding handles its <see cref="RuntimePage.Saved"/> event.</returns>
    public RuntimePage CreatePage()
    {
        var page = new RuntimePage(new RuntimeSettingsModel(SavedValues()));
        page.Saved += OnSaved;
        return page;
    }

    private RuntimeSettingsValues SavedValues()
    {
        try
        {
            return _store.Read().Runtime;
        }
        catch (SettingsSourceException)
        {
            return RuntimeSettingsValues.Defaults;
        }
    }

    private void OnSaved(object? sender, RuntimeSettingsSavedEventArgs e)
    {
        try
        {
            _store.SetRuntime(e.Values);
        }
        catch (SettingsSourceException exception)
        {
            Failed?.Invoke(this, new RuntimeSettingsSaveFailedEventArgs(exception));
        }

        _apply(e.Values);
    }
}
