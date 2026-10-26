using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.ContractTests;

/// <summary>
/// Behavioral contract every <see cref="IAcpTransport"/> must satisfy. Derive a concrete class that
/// supplies a client transport of type <typeparamref name="T"/> and a connected peer.
/// </summary>
/// <typeparam name="T">The transport type under test.</typeparam>
public abstract class AcpTransportContractTests<T> where T : IAcpTransport
{
    /// <summary>Creates an unconnected client transport and an unconnected peer wired to it.</summary>
    /// <returns>The client under test and its peer.</returns>
    protected abstract (T Client, IAcpTransport Peer) CreatePair();

    /// <summary>Connect, send, receive and close round-trip in both directions.</summary>
    [Fact]
    public async Task ConnectSendReceiveCloseRoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, peer) = CreatePair();
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(true);
            await peer.ConnectAsync(ct).ConfigureAwait(true);

            byte[] outbound = [1, 2, 3];
            await client.SendAsync(outbound, ct).ConfigureAwait(true);
            (await AsyncStreamHelpers.FirstItemAsync(peer.ReceiveAsync(ct), ct).ConfigureAwait(true)).ToArray().ShouldBe(outbound);

            byte[] reply = [9, 8];
            await peer.SendAsync(reply, ct).ConfigureAwait(true);
            (await AsyncStreamHelpers.FirstItemAsync(client.ReceiveAsync(ct), ct).ConfigureAwait(true)).ToArray().ShouldBe(reply);

            await client.CloseAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            await DisposePairAsync(client, peer).ConfigureAwait(true);
        }
    }

    /// <summary>Closing twice is safe, and the transport stays closed: the second close does not reopen it.</summary>
    [Fact]
    public async Task CloseIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, peer) = CreatePair();
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(true);
            await peer.ConnectAsync(ct).ConfigureAwait(true);

            await client.CloseAsync(ct).ConfigureAwait(true);
            await Should.NotThrowAsync(() => client.CloseAsync(ct)).ConfigureAwait(true);

            await Should.ThrowAsync<DisconnectedHarnessException>(() => client.SendAsync(new byte[] { 1 }, ct)).ConfigureAwait(true);
        }
        finally
        {
            await DisposePairAsync(client, peer).ConfigureAwait(true);
        }
    }

    /// <summary>Receiving after the local transport is closed throws DisconnectedHarnessException.</summary>
    [Fact]
    public async Task ReceiveAfterCloseThrowsDisconnectedHarnessException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, peer) = CreatePair();
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(true);
            await peer.ConnectAsync(ct).ConfigureAwait(true);
            await client.CloseAsync(ct).ConfigureAwait(true);

            await Should.ThrowAsync<DisconnectedHarnessException>(() => AsyncStreamHelpers.FirstItemAsync(client.ReceiveAsync(ct), ct)).ConfigureAwait(true);
        }
        finally
        {
            await DisposePairAsync(client, peer).ConfigureAwait(true);
        }
    }

    /// <summary>Sending after the local transport is closed throws DisconnectedHarnessException.</summary>
    [Fact]
    public async Task SendAfterCloseThrowsDisconnectedHarnessException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, peer) = CreatePair();
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(true);
            await peer.ConnectAsync(ct).ConfigureAwait(true);
            await client.CloseAsync(ct).ConfigureAwait(true);

            await Should.ThrowAsync<DisconnectedHarnessException>(() => client.SendAsync(new byte[] { 1 }, ct)).ConfigureAwait(true);
        }
        finally
        {
            await DisposePairAsync(client, peer).ConfigureAwait(true);
        }
    }

    /// <summary>A peer that closes is observed by the other side as DisconnectedHarnessException on receive.</summary>
    [Fact]
    public async Task PeerCloseSurfacesAsDisconnectedHarnessExceptionOnReceive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, peer) = CreatePair();
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(true);
            await peer.ConnectAsync(ct).ConfigureAwait(true);
            await peer.CloseAsync(ct).ConfigureAwait(true);

            await Should.ThrowAsync<DisconnectedHarnessException>(() => AsyncStreamHelpers.FirstItemAsync(client.ReceiveAsync(ct), ct)).ConfigureAwait(true);
        }
        finally
        {
            await DisposePairAsync(client, peer).ConfigureAwait(true);
        }
    }

    private static async Task DisposePairAsync(IAcpTransport client, IAcpTransport peer)
    {
        await client.DisposeAsync().ConfigureAwait(false);
        await peer.DisposeAsync().ConfigureAwait(false);
    }
}
