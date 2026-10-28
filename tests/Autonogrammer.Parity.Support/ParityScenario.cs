using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Parity.Support;

/// <summary>
/// One shared session-chat scenario: presentation-level stream events in order, and the normalized blocks every
/// head must render from them, in order.
/// </summary>
/// <param name="Name">The scenario name, unique within <see cref="ParityScenarios.All"/>.</param>
/// <param name="Events">The stream events, applied in order.</param>
/// <param name="ExpectedBlocks">The normalized blocks, in render order.</param>
public sealed record ParityScenario(string Name, IReadOnlyList<StreamEvent> Events, IReadOnlyList<ParityBlock> ExpectedBlocks)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
