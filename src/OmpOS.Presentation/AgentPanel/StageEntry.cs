using OmpOS.Contracts.Agents;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>The folded state of one stage key: the latest event and the position the stage first appeared in.</summary>
internal sealed class StageEntry
{
    /// <summary>Creates the entry for the first event of a key.</summary>
    /// <param name="latest">The first event.</param>
    /// <param name="sequence">The arrival number of the first event.</param>
    internal StageEntry(StageEvent latest, long sequence)
    {
        Latest = latest;
        FirstSeen = latest.Timestamp;
        Sequence = sequence;
    }

    /// <summary>Gets or sets the latest event for the key.</summary>
    internal StageEvent Latest { get; set; }

    /// <summary>Gets the observation time of the first event. Rows keep this position when later events update them, so a row never jumps.</summary>
    internal DateTimeOffset FirstSeen { get; }

    /// <summary>Gets the arrival number of the first event, which breaks ties between equal times.</summary>
    internal long Sequence { get; }
}
