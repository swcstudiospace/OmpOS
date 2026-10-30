namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Point in native virtual-desktop pixels.</summary>
/// <summary>Layout is sequential so the value stays a blittable coordinate.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct NativePoint
{
    /// <summary>Horizontal native pixel.</summary>
    public int X { get; init; }

    /// <summary>Vertical native pixel.</summary>
    public int Y { get; init; }
}

