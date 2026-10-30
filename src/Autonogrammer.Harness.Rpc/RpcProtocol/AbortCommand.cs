namespace Autonogrammer.Harness.Rpc;

/// <summary>Aborts the in-flight turn. The turn's <c>prompt_result</c> reports <c>aborted</c>.</summary>
public sealed record AbortCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "abort";
}
