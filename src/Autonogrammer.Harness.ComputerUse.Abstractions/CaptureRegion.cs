namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Capture rectangle in native pixels, optionally limited to one display.</summary>
public sealed record CaptureRegion
{
    /// <summary>Display id, or <see langword="null"/> when <see cref="Bounds"/> is already in virtual-desktop pixels.</summary>
    public string? DisplayId { get; init; }

    /// <summary>Native rectangle to capture.</summary>
    public required PixelRect Bounds { get; init; }
}

