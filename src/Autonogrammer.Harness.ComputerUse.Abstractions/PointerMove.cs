namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Moves the pointer to native virtual-desktop pixels.</summary>
public sealed record PointerMove : InputPrimitive
{
    /// <summary>Native X.</summary>
    public required int X { get; init; }

    /// <summary>Native Y.</summary>
    public required int Y { get; init; }
}

