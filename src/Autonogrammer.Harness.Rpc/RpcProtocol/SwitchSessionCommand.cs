namespace Autonogrammer.Harness.Rpc;

/// <summary>Switches the engine to an existing session file. omp addresses sessions by file path, not by id.</summary>
public sealed record SwitchSessionCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "switch_session";

    /// <summary>Gets the path of the session file to switch to.</summary>
    public required string SessionPath { get; init; }
}
