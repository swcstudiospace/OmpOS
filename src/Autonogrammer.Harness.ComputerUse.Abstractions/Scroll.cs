namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Scroll click. Buttons are 4 (up), 5 (down), 6 (left) or 7 (right), matching XTest.</summary>
public sealed record Scroll : InputPrimitive
{
    /// <summary>Scroll button 4, 5, 6 or 7.</summary>
    public required int Button { get; init; }

}

