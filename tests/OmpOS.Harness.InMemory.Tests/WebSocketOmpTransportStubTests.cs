using OmpOS.Harness.Abstractions;
using OmpOS.Harness.ContractTests;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.InMemory.Tests;

/// <summary>The WebSocket transport is a typed not-available stub.</summary>
public sealed class WebSocketOmpTransportStubTests
{
    /// <summary>Every operation faults with a typed NotAvailable carrying the Q-HARN-WS owner tag.</summary>
    [Fact]
    public async Task EveryOperationFaultsAsNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();

        var connect = await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.ConnectAsync(ct)).ConfigureAwait(true);
        connect.Message.ShouldContain("NotAvailable");
        connect.Message.ShouldContain("Q-HARN-WS");
        connect.InnerException.ShouldBeOfType<NotImplementedException>();

        await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.SendAsync(new byte[] { 1 }, ct)).ConfigureAwait(true);
        await Should.ThrowAsync<ProcessStartHarnessException>(() => stub.CloseAsync(ct)).ConfigureAwait(true);
        await Should.ThrowAsync<ProcessStartHarnessException>(() => AsyncStreamHelpers.FirstItemAsync(stub.ReceiveAsync(ct), ct)).ConfigureAwait(true);

        await stub.DisposeAsync().ConfigureAwait(true);
    }
}
