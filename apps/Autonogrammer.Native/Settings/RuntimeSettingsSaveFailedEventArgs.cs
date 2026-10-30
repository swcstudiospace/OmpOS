using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>Tells that a saved Runtime value could not be written to the settings file. The value is still in force for this run.</summary>
internal sealed class RuntimeSettingsSaveFailedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="RuntimeSettingsSaveFailedEventArgs"/> class.</summary>
    /// <param name="failure">Why the file could not be written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public RuntimeSettingsSaveFailedEventArgs(SettingsSourceException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets why the file could not be written.</summary>
    public SettingsSourceException Failure { get; }
}
