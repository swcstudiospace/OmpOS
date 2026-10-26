namespace OmpOS.Harness.Terminal;

/// <summary>
/// The lifecycle state of a terminal command proposal.
/// </summary>
public enum CommandStatus
{
    /// <summary>The proposal is waiting for an approval or a decline. Nothing has been spawned.</summary>
    Pending,

    /// <summary>The command ran and exited with code 0.</summary>
    Completed,

    /// <summary>The command ran and did not exit with code 0, or it could not be started or was cancelled.</summary>
    Failed,

    /// <summary>The proposal was declined. The command was never executed.</summary>
    Declined,
}
