using OmpOS.Presentation.Settings;

namespace OmpOS.Web.Services;

/// <summary>Carries the Settings page a slash command asked to open.</summary>
internal sealed class SettingsRequestedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="SettingsRequestedEventArgs"/> class.</summary>
    /// <param name="section">The page to open.</param>
    internal SettingsRequestedEventArgs(SettingsSection section) => Section = section;

    /// <summary>Gets the page to open.</summary>
    internal SettingsSection Section { get; }
}
