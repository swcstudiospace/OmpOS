using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.ContractTests;

/// <summary>Runs the ACP transport contract against the in-memory loopback pair.</summary>
public sealed class LoopbackAcpTransportContractTests : AcpTransportContractTests<LoopbackAcpTransport>
{
    /// <inheritdoc />
    protected override (LoopbackAcpTransport Client, IAcpTransport Peer) CreatePair()
    {
        var (client, server) = LoopbackAcpTransportPair.Create();
        return (client, server);
    }
}
