namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>Whether a steering entry was recorded in Substrate. A record is a note event, never a claim that Substrate delivered the message.</summary>
public enum SteeringRecordStatus
{
    /// <summary>The record is being sent.</summary>
    Recording = 0,

    /// <summary>The note event was accepted.</summary>
    Recorded = 1,

    /// <summary>Nothing was sent because no recorder is configured or Substrate is not reachable.</summary>
    NotRecorded = 2,

    /// <summary>The recorder threw, timed out or was cancelled. The message itself is still queued.</summary>
    RecordFailed = 3,
}
