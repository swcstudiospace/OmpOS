using OmpOS.Presentation.Settings;

namespace OmpOS.Native;

/// <summary>Arguments of <see cref="MainView.SettingsRequested"/>: the Settings page the request names.</summary>
public sealed class SettingsRequestedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="SettingsRequestedEventArgs"/> class.</summary>
    /// <param name="section">The page to open, or <see langword="null"/> for the window's first page.</param>
    public SettingsRequestedEventArgs(SettingsSection? section)
    {
        Section = section;
    }

    /// <summary>Gets the page to open, or <see langword="null"/> for the window's first page.</summary>
    public SettingsSection? Section { get; }
}
