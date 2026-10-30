namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Axis-aligned rectangle in native virtual-desktop pixels. Origins may be negative.</summary>
/// <summary>Layout is sequential so the value stays a blittable coordinate.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct PixelRect
{
    /// <summary>Left edge in native pixels.</summary>
    public int X { get; init; }

    /// <summary>Top edge in native pixels.</summary>
    public int Y { get; init; }

    /// <summary>Width in native pixels. Must be positive when used as a display.</summary>
    public int Width { get; init; }

    /// <summary>Height in native pixels. Must be positive when used as a display.</summary>
    public int Height { get; init; }
}

