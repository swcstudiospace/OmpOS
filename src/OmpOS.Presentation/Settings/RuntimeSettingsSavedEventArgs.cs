namespace OmpOS.Presentation.Settings;

/// <summary>The values of the Runtime section after a successful save.</summary>
public sealed class RuntimeSettingsSavedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="RuntimeSettingsSavedEventArgs"/> class.</summary>
    /// <param name="values">The saved values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public RuntimeSettingsSavedEventArgs(RuntimeSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }

    /// <summary>Gets the saved values.</summary>
    public RuntimeSettingsValues Values { get; }
}
