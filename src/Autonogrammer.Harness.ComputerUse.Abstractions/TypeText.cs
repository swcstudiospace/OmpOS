namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Types a short text run. The executor splits long text; the backend does not decide policy.</summary>
public sealed record TypeText : InputPrimitive
{
    /// <summary>Characters to inject. Audit must not copy this string into a failure message.</summary>
    public required string Text { get; init; }
}

