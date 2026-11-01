namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>One display. Coordinates crossing the backend are native pixels; <see cref="ScaleFactor"/> is applied only by <see cref="CoordinateMap"/>.</summary>
public sealed record DisplayInfo
{
    /// <summary>Stable display id for this session.</summary>
    public required string Id { get; init; }

    /// <summary>Native bounds, including a negative virtual-desktop origin.</summary>
    public required PixelRect Bounds { get; init; }

    /// <summary>Native pixels per model unit. Physical-pixel hosts use 1. Point-based hosts carry the points-to-pixels ratio, often 2.</summary>
    public required double ScaleFactor { get; init; }

    /// <summary><see langword="true"/> for the primary display.</summary>
    public required bool Primary { get; init; }
}

