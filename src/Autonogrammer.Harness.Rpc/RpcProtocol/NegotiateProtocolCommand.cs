namespace Autonogrammer.Harness.Rpc;

/// <summary>Asks omp to speak a protocol version after the <c>ready</c> frame.</summary>
public sealed record NegotiateProtocolCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "negotiate_protocol";

    /// <summary>Gets the requested protocol version.</summary>
    public int ProtocolVersion { get; init; }
}
