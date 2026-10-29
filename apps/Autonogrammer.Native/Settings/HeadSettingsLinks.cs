using Autonogrammer.Presentation.Cloud;
using Avalonia.Controls;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The two things a page of the Settings window needs from the head that exist only after the pages were described: the main window (the
/// owner of the About window) and the listener of the Cloud page's "open in Agents" request. <see cref="HeadSettings"/> creates it before the
/// window options and fills it when the window is attached.
/// </summary>
internal sealed class HeadSettingsLinks
{
    /// <summary>Occurs when the Cloud page asks to open the session of a cloud agent.</summary>
    public event EventHandler<CloudAgentOpenEventArgs>? CloudAgentOpenRequested;

    /// <summary>Gets or sets the main window the About window opens over, or null before the head is attached.</summary>
    public Window? Owner { get; set; }

    /// <summary>Raises <see cref="CloudAgentOpenRequested"/>.</summary>
    /// <param name="arguments">The agent and its session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    public void RaiseCloudAgentOpen(CloudAgentOpenEventArgs arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        CloudAgentOpenRequested?.Invoke(this, arguments);
    }
}
