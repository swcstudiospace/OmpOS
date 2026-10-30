namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A point in a tool call that the host publishes.</summary>
public enum CoworkToolEventKind
{
    /// <summary>The host accepted the call and started the sequence.</summary>
    Started = 0,

    /// <summary>The host is waiting for a person.</summary>
    ApprovalRequested = 1,

    /// <summary>The call finished without an error result.</summary>
    Completed = 2,

    /// <summary>The call finished as a failure.</summary>
    Failed = 3,

    /// <summary>The call was refused or not approved.</summary>
    Denied = 4,
}
