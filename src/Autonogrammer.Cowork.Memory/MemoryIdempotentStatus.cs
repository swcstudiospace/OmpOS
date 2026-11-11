namespace Autonogrammer.Cowork.Memory;

/// <summary>Possible durable create outcomes.</summary>
public enum MemoryIdempotentStatus
{
    /// <summary>A new entry was committed.</summary>
    Created,
    /// <summary>An existing committed entry was read.</summary>
    Replayed,
    /// <summary>The key belongs to another request.</summary>
    Conflict,
    /// <summary>The matching entry has been deleted.</summary>
    Gone,
    /// <summary>A policy, pause, cap, or ownership check refused the create.</summary>
    Refused,
    /// <summary>The result could not be determined; retry with the same key.</summary>
    Unavailable,
}
