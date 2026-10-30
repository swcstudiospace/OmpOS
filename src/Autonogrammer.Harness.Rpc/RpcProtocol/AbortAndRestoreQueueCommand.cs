namespace Autonogrammer.Harness.Rpc;

/// <summary>Aborts the in-flight turn and withdraws queued user input back to the caller.</summary>
public sealed record AbortAndRestoreQueueCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "abort_and_restore_queue";
}
