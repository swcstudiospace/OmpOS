namespace OmpOS.Contracts.Agents;

/// <summary>
/// The workflow phase a session works on, as the workflow engine's planning files record it.
/// </summary>
public sealed record GsdPhaseInfo
{
    /// <summary>Gets the phase number as written in the roadmap, for example <c>25.6</c>.</summary>
    public required string Number { get; init; }

    /// <summary>Gets the phase name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the phase status as the planning files record it, for example <c>complete</c> or <c>in progress</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the number of completed plans in the phase.</summary>
    public int PlansComplete { get; init; }

    /// <summary>Gets the total number of plans in the phase.</summary>
    public int PlansTotal { get; init; }
}
