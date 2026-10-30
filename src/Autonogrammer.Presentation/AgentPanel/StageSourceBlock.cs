using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>One source block of the Pipeline tab: the source name, its rolled-up status and its rows in display order.</summary>
public sealed record StageSourceBlock
{
    /// <summary>Gets the source. An undefined value is the block of rows from an unknown source.</summary>
    public required StageSource Source { get; init; }

    /// <summary>Gets the block header: Engine, Ultrathink, Swarm, Substrate, GSD or Other.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the rows in display order: top-level rows by first observation, each followed by its children.</summary>
    public required IReadOnlyList<StageRow> Rows { get; init; }

    /// <summary>Gets the rolled-up status of the top-level rows, or null while the source has no events.</summary>
    public StageStatus? RollUpStatus { get; init; }

    /// <summary>Gets the rolled-up status in words, or null while the source has no events.</summary>
    public string? RollUpText { get; init; }

    /// <summary>Gets the glyph key of the rolled-up status, or null while the source has no events.</summary>
    public string? RollUpIconKey { get; init; }

    /// <summary>Gets the line shown while the source has no events, or null once it has some.</summary>
    public string? EmptyText { get; init; }
}
