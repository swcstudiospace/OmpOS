using Avalonia.Controls;

namespace Autonogrammer.Native.Views;

/// <summary>Opens the About window. The head holds one launcher; the rail footer button, the <c>app.about</c> action and the application menu all call it.</summary>
public interface IAboutLauncher
{
    /// <summary>Shows the About window, or brings it to the front when it is already open.</summary>
    /// <param name="owner">The window to centre over, or null for the centre of the screen.</param>
    void Show(Window? owner);
}
