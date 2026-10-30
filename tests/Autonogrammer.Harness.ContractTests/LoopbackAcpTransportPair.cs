namespace Autonogrammer.Harness.ContractTests;

/// <summary>Creates connected <see cref="LoopbackAcpTransport"/> pairs for contract tests.</summary>
internal static class LoopbackAcpTransportPair
{
    /// <summary>Creates a new pair whose endpoints are linked to each other.</summary>
    /// <returns>The client and server endpoints.</returns>
    public static (LoopbackAcpTransport Client, LoopbackAcpTransport Server) Create()
    {
        var client = new LoopbackAcpTransport();
        var server = new LoopbackAcpTransport();
        client.LinkTo(server);
        server.LinkTo(client);
        return (client, server);
    }
}
