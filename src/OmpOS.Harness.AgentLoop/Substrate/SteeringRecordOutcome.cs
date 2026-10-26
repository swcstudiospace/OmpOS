namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>What became of an attempt to record a steering action.</summary>
public enum SteeringRecordOutcome
{
    /// <summary>The ledger stored the event.</summary>
    Recorded,

    /// <summary>Nothing was recorded because the Substrate is not available.</summary>
    Skipped,

    /// <summary>Recording was attempted and failed, or the record was not valid.</summary>
    Failed,
}
