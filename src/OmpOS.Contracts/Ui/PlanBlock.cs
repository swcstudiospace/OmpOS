namespace OmpOS.Contracts.Ui;

/// <summary>
/// A plan panel block (kind "plan").
/// </summary>
public sealed record PlanBlock : UiBlock
{
    /// <summary>Gets the ordered plan steps.</summary>
    public required IReadOnlyList<string> Steps { get; init; }
}
