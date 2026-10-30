namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Presses or releases a pointer button. Buttons are 1, 2 or 3.</summary>
public sealed record PointerButton : InputPrimitive
{
    /// <summary>Button 1, 2 or 3.</summary>
    public required int Button { get; init; }

    /// <summary><see langword="true"/> for down, <see langword="false"/> for up.</summary>
    public required bool Down { get; init; }

}

