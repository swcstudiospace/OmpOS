namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Pointer position in native virtual-desktop pixels, plus which buttons are down.</summary>
public sealed record PointerState
{
    /// <summary>Native X.</summary>
    public required int X { get; init; }

    /// <summary>Native Y.</summary>
    public required int Y { get; init; }

    /// <summary>Buttons currently down. Bits 0, 1 and 2 are buttons 1, 2 and 3.</summary>
    public required int Buttons { get; init; }
}

