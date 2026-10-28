namespace Autonogrammer.Harness.Rpc;

/// <summary>Reads the engine's current session state, including the active model.</summary>
public sealed record GetStateCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "get_state";
}
