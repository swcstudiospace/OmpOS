namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Point in the model's scaled coordinate space for one display.</summary>
/// <summary>Layout is sequential so the value stays a blittable coordinate.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct ModelPoint
{
    /// <summary>Horizontal offset from the display origin, in scaled units.</summary>
    public double X { get; init; }

    /// <summary>Vertical offset from the display origin, in scaled units.</summary>
    public double Y { get; init; }
}

