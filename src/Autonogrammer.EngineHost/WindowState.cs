namespace Autonogrammer.EngineHost;

/// <summary>
/// The persistable subset of native window geometry.
/// </summary>
public sealed record WindowState
{
    /// <summary>Gets the window width in device-independent pixels.</summary>
    public double Width { get; init; }

    /// <summary>Gets the window height in device-independent pixels.</summary>
    public double Height { get; init; }

    /// <summary>Gets the window X offset, or null when the platform places the window.</summary>
    public double? X { get; init; }

    /// <summary>Gets the window Y offset, or null when the platform places the window.</summary>
    public double? Y { get; init; }

    /// <summary>Gets whether the window was maximized.</summary>
    public bool IsMaximized { get; init; }
}
