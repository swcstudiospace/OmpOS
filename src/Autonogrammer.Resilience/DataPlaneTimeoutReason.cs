namespace Autonogrammer.Resilience;

/// <summary>Why a pipeline refused to return the result of a call.</summary>
public enum DataPlaneTimeoutReason
{
    /// <summary>The pipeline deadline expired before the call completed.</summary>
    Timeout = 0,

    /// <summary>The circuit breaker is open, so the call was refused without reaching the dependency.</summary>
    CircuitOpen = 1,

    /// <summary>The bulkhead of the operation class is full, so the call was refused without reaching the dependency.</summary>
    BulkheadFull = 2,
}
