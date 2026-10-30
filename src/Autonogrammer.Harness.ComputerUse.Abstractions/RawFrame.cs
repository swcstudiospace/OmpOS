namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>One captured frame. Pixels are native BGRA, top-down, with <see cref="Stride"/> bytes per row.</summary>
public sealed record RawFrame
{
    /// <summary>BGRA pixel bytes. Not a window title and not typed text.</summary>
    public required ReadOnlyMemory<byte> Pixels { get; init; }

    /// <summary>Width in native pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height in native pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Bytes per row. At least <see cref="Width"/> times 4.</summary>
    public required int Stride { get; init; }
}

