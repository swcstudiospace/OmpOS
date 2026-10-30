using Avalonia.Controls;
using Avalonia.Platform;

namespace Autonogrammer.Native.Views.Branding;

/// <summary>
/// The Autonogrammer window icon: the brand mark (<c>assets/brand</c>, PX-02) shipped as an Avalonia resource of this assembly. Every head window takes it through
/// <see cref="Create"/> or <see cref="Apply"/>, so a window shows the mark in its title bar, task switcher and taskbar instead of the toolkit default.
/// </summary>
public static class AppIcon
{
    /// <summary>The address of the mark resource, a 256 by 256 PNG.</summary>
    public const string ResourceUri = "avares://Autonogrammer.Native.Views/Assets/Brand/autonogrammer-mark-256.png";

    /// <summary>Tells whether the mark resource is part of this assembly.</summary>
    /// <returns><see langword="true"/> when <see cref="Create"/> can read it.</returns>
    public static bool Exists() => AssetLoader.Exists(new Uri(ResourceUri));

    /// <summary>Reads the mark into a window icon.</summary>
    /// <returns>A new icon. The caller gives it to one window.</returns>
    /// <exception cref="FileNotFoundException">The resource is missing from the assembly.</exception>
    public static WindowIcon Create()
    {
        using var stream = AssetLoader.Open(new Uri(ResourceUri));
        return new WindowIcon(stream);
    }

    /// <summary>Sets the icon of a window.</summary>
    /// <param name="window">The window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is null.</exception>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Icon = Create();
    }
}
