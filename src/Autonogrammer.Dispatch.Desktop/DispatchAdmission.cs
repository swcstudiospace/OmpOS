namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The dedupe decision for one verified command.</summary>
public enum DispatchAdmission
{
    /// <summary>The command is new. It is persisted and may now be acknowledged and executed.</summary>
    Execute,

    /// <summary>The command was already applied. It is acknowledged as a duplicate and never executed.</summary>
    Duplicate,

    /// <summary>The nonce belongs to a different command already applied. It is refused.</summary>
    Replayed,
}
