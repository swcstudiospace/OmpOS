using OmpOS.Contracts.Agents;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>One pill of the pipeline strip: a plugin source with its glyph and short status text, for example <c>Swarm 3 of 5</c>.</summary>
public sealed record PipelinePill
{
    /// <summary>Gets the source the pill shows.</summary>
    public required StageSource Source { get; init; }

    /// <summary>Gets the pill text, for example <c>Ultrathink planned</c>.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the rolled-up status, or null while the source has no events (the pill then reads <c>waiting</c>).</summary>
    public StageStatus? Status { get; init; }

    /// <summary>Gets the glyph key of the status, from <see cref="StatusIcon"/>.</summary>
    public required string IconKey { get; init; }
}
