using System.Diagnostics.CodeAnalysis;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.ContractTests;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.InMemory.Tests;

/// <summary>The WebSocket transport is a typed not-available stub.</summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns the stub transport.")]
public sealed class WebSocketOmpTransportStubTests
{
    /// <summary>Connect, send and receive fault with a typed NotAvailable carrying the Q-HARN-WS owner tag.</summary>
    [Fact]
    public async Task OperationsFaultAsNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();

        var connect = await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.ConnectAsync(ct)).ConfigureAwait(true);
        connect.Message.ShouldContain("NotAvailable");
        connect.Message.ShouldContain("Q-HARN-WS");
        connect.InnerException.ShouldBeOfType<NotImplementedException>();

        await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.SendAsync(new byte[] { 1 }, ct)).ConfigureAwait(true);
        await Should.ThrowAsync<ProcessStartHarnessException>(() => AsyncStreamHelpers.FirstItemAsync(stub.ReceiveAsync(ct), ct)).ConfigureAwait(true);

        await stub.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Closing an unconnected stub succeeds: there is nothing to close, and the stub still reports itself as not available.</summary>
    [Fact]
    public async Task CloseOnUnconnectedStubSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();

        await Should.NotThrowAsync(() => stub.CloseAsync(ct)).ConfigureAwait(true);

        var connect = await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.ConnectAsync(ct)).ConfigureAwait(true);
        connect.Message.ShouldContain("Q-HARN-WS");

        await stub.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Disposing a session built on the stub does not throw, even after its connect fails.</summary>
    [Fact]
    public async Task DisposeSessionBuiltOnStubDoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new AcpAgentSession(
            new WebSocketOmpTransportStub(),
            new AcpClientOptions { WorkingDirectory = Directory.GetCurrentDirectory() });
        await using var sessionScope = session.ConfigureAwait(true);

        await Should.ThrowAsync<ProcessStartHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);
        session.State.ShouldBe(HarnessState.Disposed);
    }
}
