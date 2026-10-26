using Avalonia.Controls;

namespace OmpOS.Native.Settings;

/// <summary>One Settings window as the host sees it: it can be shown over an owner, brought forward and closed. A test replaces the real window with a fake.</summary>
internal interface ISettingsWindowHandle
{
    /// <summary>Occurs once, after the window closed.</summary>
    event EventHandler? Closed;

    /// <summary>Shows the window without blocking its owner, centred over the owner when there is one.</summary>
    /// <param name="owner">The window it opens over, or null.</param>
    void Show(Window? owner);

    /// <summary>Brings the open window forward and gives it the focus.</summary>
    void Activate();

    /// <summary>Closes the window without asking about unsaved changes: the application is closing, so they are dropped.</summary>
    void CloseDiscardingChanges();
}
