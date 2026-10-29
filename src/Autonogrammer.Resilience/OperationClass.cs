namespace Autonogrammer.Resilience;

/// <summary>Which bulkhead and which command timeout a call uses.</summary>
public enum OperationClass
{
    /// <summary>A call made on behalf of a waiting request: short timeout, the large bulkhead.</summary>
    Interactive = 0,

    /// <summary>A call made by a background worker: long timeout, the small bulkhead, so it cannot starve requests.</summary>
    Background = 1,
}
